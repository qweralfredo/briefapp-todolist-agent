using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

// ── Result types ───────────────────────────────────────────────────────────────

public record DlqEntryDto(
    Guid            Id,
    Guid            BoxId,
    string          OriginalTopic,
    string          OriginalTaskId,
    string          FailureReason,
    int             RetryCount,
    DlqStatus       Status,
    DateTimeOffset  FirstFailedAt,
    DateTimeOffset  LastFailedAt,
    string?         OldestEntryAge = null
);

public record DlqStatsDto(
    int    Total,
    int    Pending,
    int    Retrying,
    int    Resolved,
    int    Quarantined,
    string OldestEntryAge
);

public record DlqPageResult(
    int              Page,
    int              PageSize,
    int              TotalCount,
    List<DlqEntryDto> Items
);

// ── ST-37: DeadLetterQueueService ─────────────────────────────────────────────

/// <summary>
/// ST-37: Manages the Dead Letter Queue lifecycle.
/// NACK with retryCount >= maxRetries → MoveToDeadLetter.
/// Poison detection: auto-quarantine when RetryCount >= 5.
/// </summary>
public sealed class DeadLetterQueueService
{
    private const int PoisonMessageThreshold = 5;
    private const int DlqAlertThreshold      = 10;

    private readonly AppDbContext                       _db;
    private readonly TansuPublisherService              _publisher;
    private readonly ILogger<DeadLetterQueueService>    _logger;

    public DeadLetterQueueService(
        AppDbContext                    db,
        TansuPublisherService           publisher,
        ILogger<DeadLetterQueueService> logger)
    {
        _db        = db;
        _publisher = publisher;
        _logger    = logger;
    }

    // ── ST-37: MoveToDeadLetter ───────────────────────────────────────────────

    /// <summary>Persists a failed task in the DLQ. Auto-quarantines poison messages.</summary>
    public async Task<DlqEntryEntity> MoveToDeadLetterAsync(
        TaskMessageEntity task,
        string            failureReason,
        int               retryCount,
        CancellationToken ct = default)
    {
        var status = retryCount >= PoisonMessageThreshold
            ? DlqStatus.Quarantined
            : DlqStatus.Pending;

        var entry = new DlqEntryEntity
        {
            BoxId          = task.BoxId,
            OriginalTopic  = task.Topic,
            TaskPayload    = JsonSerializer.Serialize(task.Payload),
            OriginalTaskId = task.Id.ToString(),
            FailureReason  = failureReason,
            RetryCount     = retryCount,
            FirstFailedAt  = DateTimeOffset.UtcNow,
            LastFailedAt   = DateTimeOffset.UtcNow,
            Status         = status,
        };

        _db.DlqEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        if (status == DlqStatus.Quarantined)
            _logger.LogWarning("Poison message quarantined: Task={TaskId} Retries={Retries}", task.Id, retryCount);
        else
            _logger.LogWarning("Task moved to DLQ: Task={TaskId} Reason={Reason}", task.Id, failureReason);

        // Alert if DLQ exceeds threshold
        await CheckDlqAlertAsync(task.BoxId, ct);

        return entry;
    }

    // ── ST-37: RetryFromDlq ───────────────────────────────────────────────────

    /// <summary>Resubmits a DLQ entry to the original topic via Tansu.</summary>
    public async Task<bool> RetryFromDlqAsync(Guid dlqId, CancellationToken ct = default)
    {
        var entry = await _db.DlqEntries.FindAsync([dlqId], ct);
        if (entry is null) return false;
        if (entry.Status == DlqStatus.Quarantined)
        {
            _logger.LogWarning("RetryFromDlq: entry {Id} is Quarantined — refusing retry.", dlqId);
            return false;
        }

        object payload;
        try { payload = JsonSerializer.Deserialize<object>(entry.TaskPayload) ?? new { }; }
        catch { payload = new { raw = entry.TaskPayload }; }

        entry.Status      = DlqStatus.Retrying;
        entry.LastFailedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _publisher.PublishTaskAsync(entry.BoxId, payload, "dlq-retry", null, 3,
            topicOverride: entry.OriginalTopic, ct: ct);
        _logger.LogInformation("DLQ retry submitted for entry {Id} (task={TaskId})", dlqId, entry.OriginalTaskId);
        return true;
    }

    // ── ST-37: DrainDlq ──────────────────────────────────────────────────────

    /// <summary>Retries all Pending DLQ entries (optionally filtered by boxId).</summary>
    public async Task<int> DrainDlqAsync(Guid? boxId, CancellationToken ct = default)
    {
        var query = _db.DlqEntries
            .Where(e => e.Status == DlqStatus.Pending);

        if (boxId.HasValue)
            query = query.Where(e => e.BoxId == boxId.Value);

        var entries = await query.ToListAsync(ct);
        int count   = 0;

        foreach (var entry in entries)
        {
            if (await RetryFromDlqAsync(entry.Id, ct)) count++;
        }

        _logger.LogInformation("DLQ drain: {Count} entries resubmitted (boxId={BoxId})", count, boxId);
        return count;
    }

    // ── ST-37: QuarantineEntry ────────────────────────────────────────────────

    /// <summary>Manually quarantines a DLQ entry (prevents retry).</summary>
    public async Task<bool> QuarantineEntryAsync(Guid dlqId, CancellationToken ct = default)
    {
        var entry = await _db.DlqEntries.FindAsync([dlqId], ct);
        if (entry is null) return false;

        entry.Status = DlqStatus.Quarantined;
        await _db.SaveChangesAsync(ct);
        _logger.LogWarning("DLQ entry {Id} quarantined manually.", dlqId);
        return true;
    }

    // ── ST-37: GetDlqStats ────────────────────────────────────────────────────

    public async Task<DlqStatsDto> GetDlqStatsAsync(Guid? boxId, CancellationToken ct = default)
    {
        var query = _db.DlqEntries.AsNoTracking();
        if (boxId.HasValue) query = query.Where(e => e.BoxId == boxId.Value);

        var grouped = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total       = g.Count(),
                Pending     = g.Count(e => e.Status == DlqStatus.Pending),
                Retrying    = g.Count(e => e.Status == DlqStatus.Retrying),
                Resolved    = g.Count(e => e.Status == DlqStatus.Resolved),
                Quarantined = g.Count(e => e.Status == DlqStatus.Quarantined),
                OldestAt    = g.Min(e => (DateTimeOffset?)e.FirstFailedAt),
            })
            .FirstOrDefaultAsync(ct);

        var age = grouped?.OldestAt is DateTimeOffset oldest
            ? FormatAge(DateTimeOffset.UtcNow - oldest)
            : "–";

        return new DlqStatsDto(
            grouped?.Total ?? 0,
            grouped?.Pending ?? 0,
            grouped?.Retrying ?? 0,
            grouped?.Resolved ?? 0,
            grouped?.Quarantined ?? 0,
            age);
    }

    // ── ST-37: GetPaged ───────────────────────────────────────────────────────

    public async Task<DlqPageResult> GetPagedAsync(
        Guid?     boxId,
        DlqStatus? status,
        int        page     = 1,
        int        pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.DlqEntries.AsNoTracking();
        if (boxId.HasValue)  query = query.Where(e => e.BoxId  == boxId.Value);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.LastFailedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new DlqEntryDto(
                e.Id, e.BoxId, e.OriginalTopic, e.OriginalTaskId,
                e.FailureReason, e.RetryCount, e.Status,
                e.FirstFailedAt, e.LastFailedAt, null))
            .ToListAsync(ct);

        return new DlqPageResult(page, pageSize, total, items);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task CheckDlqAlertAsync(Guid boxId, CancellationToken ct)
    {
        var count = await _db.DlqEntries
            .CountAsync(e => e.BoxId == boxId && e.Status == DlqStatus.Pending, ct);

        if (count >= DlqAlertThreshold)
        {
            _logger.LogWarning("DLQ alert: Box {BoxId} has {Count} pending entries.", boxId, count);
            try
            {
                await _publisher.PublishTaskAsync(boxId, new
                {
                    alertType = "dlq-size",
                    boxId,
                    pendingCount = count,
                    threshold = DlqAlertThreshold,
                    timestamp = DateTimeOffset.UtcNow,
                }, "system", null, 1, topicOverride: "briefapp.system.dlq-alert", ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish DLQ alert.");
            }
        }
    }

    private static string FormatAge(TimeSpan age) =>
        age.TotalDays  >= 1 ? $"{(int)age.TotalDays}d {age.Hours}h"  :
        age.TotalHours >= 1 ? $"{(int)age.TotalHours}h {age.Minutes}m" :
        $"{(int)age.TotalMinutes}m";
}
