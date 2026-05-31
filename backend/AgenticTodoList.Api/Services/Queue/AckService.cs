using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

/// <summary>
/// ST-30: Processes ACK/NACK from worker agents.
///   ACK: marks task Done, releases lock, records metrics.
///   NACK: releases lock, increments retry_count, requeues with exponential backoff
///         or moves to DLQ after max_retries.
/// </summary>
public sealed class AckService
{
    // Exponential backoff delays: attempt 1→1min, 2→5min, 3→15min
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private readonly AppDbContext          _db;
    private readonly TansuPublisherService _publisher;
    private readonly LockService           _lockService;
    private readonly ILogger<AckService>   _logger;

    public AckService(
        AppDbContext db,
        TansuPublisherService publisher,
        LockService lockService,
        ILogger<AckService> logger)
    {
        _db        = db;
        _publisher = publisher;
        _lockService = lockService;
        _logger    = logger;
    }

    public async Task<AckProcessResult> ProcessAckAsync(AckPayload ack, CancellationToken ct = default)
    {
        if (!Guid.TryParse(ack.TaskId, out var taskGuid))
            throw new ArgumentException($"Invalid TaskId: {ack.TaskId}");

        var task = await _db.TaskMessages.FindAsync([taskGuid], ct)
            ?? throw new KeyNotFoundException($"Task {ack.TaskId} not found.");

        // Release the lock (best effort)
        var activeLock = await _lockService.GetActiveLockAsync(ack.TaskId, ct);
        if (activeLock is not null)
            await _lockService.ReleaseLockAsync(activeLock.Id, ack.WorkerId, ct: ct);

        if (ack.Status.Equals("ack", StringComparison.OrdinalIgnoreCase))
            return await ProcessSuccessAsync(task, ack, ct);
        else
            return await ProcessFailureAsync(task, ack, ct);
    }

    // ── ACK (success) ──────────────────────────────────────────────────────────

    private async Task<AckProcessResult> ProcessSuccessAsync(TaskMessageEntity task, AckPayload ack, CancellationToken ct)
    {
        task.Status      = TaskMessageStatus.Completed;
        task.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Task {Id} ACK — completed by {Worker}.", task.Id, ack.WorkerId);
        return new AckProcessResult(true, "completed", task.RetryCount);
    }

    // ── NACK (failure) ─────────────────────────────────────────────────────────

    private async Task<AckProcessResult> ProcessFailureAsync(TaskMessageEntity task, AckPayload ack, CancellationToken ct)
    {
        task.RetryCount++;

        if (task.RetryCount >= task.MaxRetries)
        {
            task.Status      = TaskMessageStatus.Failed;
            task.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Task {Id} NACK after {Retries} attempts — moved to DLQ (category={Cat}).",
                task.Id, task.RetryCount, ack.Error?.Category);

            // DLQ publication is handled externally by DeadLetterQueueService (BOX2-04)
            return new AckProcessResult(false, "moved_to_dlq", task.RetryCount,
                $"Exceeded max retries ({task.MaxRetries}). Category: {ack.Error?.Category}");
        }

        // Exponential backoff retry
        var delayIndex = Math.Min(task.RetryCount - 1, RetryDelays.Length - 1);
        var delay      = RetryDelays[delayIndex];

        await _publisher.RequeueAsync(task, delay, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Task {Id} NACK — requeuing (attempt {Retry}/{Max}, delay={Delay}s, category={Cat}).",
            task.Id, task.RetryCount, task.MaxRetries, delay.TotalSeconds, ack.Error?.Category);

        return new AckProcessResult(false, "retrying", task.RetryCount,
            $"Retry {task.RetryCount}/{task.MaxRetries} in {delay.TotalMinutes}min.");
    }
}
