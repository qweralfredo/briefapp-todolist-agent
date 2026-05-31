using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

// ── DTO ───────────────────────────────────────────────────────────────────────

public record BoxQueueStats(Guid BoxId, int Pending, int Processing);

public record QueueDashboardDto(
    int                 PendingCount,
    int                 ProcessingCount,
    int                 CompletedToday,
    int                 FailedToday,
    int                 DlqSize,
    double              AvgProcessingMs,
    double              ThroughputPerMin,
    int                 ActiveLocks,
    BoxQueueStats[]     BoxStats,
    DateTimeOffset      CapturedAt
);

// ── ST-47: QueueDashboardService ─────────────────────────────────────────────

/// <summary>
/// ST-47: Computes live queue metrics for the Transactional Dashboard.
/// Aggregates TaskMessages, TaskLocks, and DlqEntries in a single query pass.
/// </summary>
public sealed class QueueDashboardService
{
    private readonly AppDbContext                    _db;
    private readonly ILogger<QueueDashboardService>  _logger;

    public QueueDashboardService(AppDbContext db, ILogger<QueueDashboardService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>Computes a live snapshot of the queue across all boxes.</summary>
    public async Task<QueueDashboardDto> GetLiveStatsAsync(
        Guid?             boxId = null,
        CancellationToken ct    = default)
    {
        var today = DateTimeOffset.UtcNow.Date;

        // ── TaskMessage aggregates ─────────────────────────────────────────────
        var msgQuery = _db.TaskMessages.AsNoTracking();
        if (boxId.HasValue) msgQuery = msgQuery.Where(m => m.BoxId == boxId.Value);

        var msgStats = await msgQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Pending         = g.Count(m => m.Status == TaskMessageStatus.Pending),
                Processing      = g.Count(m => m.Status == TaskMessageStatus.Processing),
                CompletedToday  = g.Count(m => m.Status == TaskMessageStatus.Completed
                                             && m.CompletedAt >= today),
                FailedToday     = g.Count(m => m.Status == TaskMessageStatus.Failed
                                             && m.CompletedAt >= today),
                AvgMs           = g.Where(m => m.DurationMs.HasValue)
                                   .Average(m => (double?)m.DurationMs) ?? 0.0,
            })
            .FirstOrDefaultAsync(ct);

        // ── Throughput: completed in last 60 seconds ──────────────────────────
        var oneMinAgo = DateTimeOffset.UtcNow.AddMinutes(-1);
        var throughput = await (boxId.HasValue
            ? _db.TaskMessages.Where(m => m.BoxId == boxId.Value && m.CompletedAt >= oneMinAgo)
            : _db.TaskMessages.Where(m => m.CompletedAt >= oneMinAgo)
        ).CountAsync(ct);

        // ── Active Locks ───────────────────────────────────────────────────────
        var activeLocks = await _db.TaskLocks
            .CountAsync(l => l.Status == LockStatus.Active, ct);

        // ── DLQ Size ───────────────────────────────────────────────────────────
        var dlqQuery = _db.DlqEntries.Where(e => e.Status == DlqStatus.Pending);
        if (boxId.HasValue) dlqQuery = dlqQuery.Where(e => e.BoxId == boxId.Value);
        var dlqSize = await dlqQuery.CountAsync(ct);

        // ── Per-box breakdown ─────────────────────────────────────────────────
        BoxQueueStats[] boxStats;
        if (boxId.HasValue)
        {
            boxStats = [];
        }
        else
        {
            boxStats = await _db.TaskMessages
                .AsNoTracking()
                .Where(m => m.Status == TaskMessageStatus.Pending || m.Status == TaskMessageStatus.Processing)
                .GroupBy(m => m.BoxId)
                .Select(g => new BoxQueueStats(g.Key,
                    g.Count(m => m.Status == TaskMessageStatus.Pending),
                    g.Count(m => m.Status == TaskMessageStatus.Processing)))
                .ToArrayAsync(ct);
        }

        return new QueueDashboardDto(
            msgStats?.Pending         ?? 0,
            msgStats?.Processing      ?? 0,
            msgStats?.CompletedToday  ?? 0,
            msgStats?.FailedToday     ?? 0,
            dlqSize,
            msgStats?.AvgMs           ?? 0.0,
            throughput,
            activeLocks,
            boxStats,
            DateTimeOffset.UtcNow);
    }
}
