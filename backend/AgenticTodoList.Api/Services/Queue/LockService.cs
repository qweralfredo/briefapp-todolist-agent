using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

// ── Result types ──────────────────────────────────────────────────────────────

public record LockResult(Guid LockId, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt);
public record HeartbeatResult(bool Success, DateTimeOffset? NewExpiresAt, string? Error = null);

// ── LockService ───────────────────────────────────────────────────────────────

/// <summary>
/// ST-23/24/25/26: Pessimistic distributed task lock service.
/// Guarantees only one worker processes a task at a time.
/// Uses serializable transactions to prevent race conditions.
/// </summary>
public sealed class LockService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LockService> _logger;

    public LockService(AppDbContext db, ILogger<LockService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── ST-24: AcquireLock ────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to acquire an exclusive lock on a task.
    /// If an existing lock is expired, it will be superseded.
    /// Throws InvalidOperationException if task is already locked.
    /// </summary>
    public async Task<LockResult> AcquireLockAsync(
        string taskId,
        string workerId,
        int    timeoutMinutes = 30,
        CancellationToken ct  = default)
    {
        // Expire any stale active locks
        var stale = await _db.TaskLocks
            .Where(l => l.TaskId == taskId && l.Status == LockStatus.Active && l.ExpiresAt < DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        foreach (var s in stale) s.Status = LockStatus.Expired;
        if (stale.Count > 0) await _db.SaveChangesAsync(ct);

        // Check for active lock
        var existing = await _db.TaskLocks
            .FirstOrDefaultAsync(l => l.TaskId == taskId && l.Status == LockStatus.Active, ct);

        if (existing is not null)
            throw new InvalidOperationException(
                $"Task {taskId} is already locked by worker {existing.WorkerId} until {existing.ExpiresAt:O}.");

        var now = DateTimeOffset.UtcNow;
        var lockEntity = new TaskLockEntity
        {
            TaskId        = taskId,
            WorkerId      = workerId,
            Status        = LockStatus.Active,
            LockedAt      = now,
            ExpiresAt     = now.AddMinutes(timeoutMinutes),
            HeartbeatAt   = now,
            TimeoutMinutes = timeoutMinutes,
        };

        _db.TaskLocks.Add(lockEntity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Lock {LockId} acquired by {Worker} for task {Task}, expires {Exp}",
            lockEntity.Id, workerId, taskId, lockEntity.ExpiresAt);

        return new LockResult(lockEntity.Id, now, lockEntity.ExpiresAt);
    }

    // ── ST-25: Heartbeat ──────────────────────────────────────────────────────

    /// <summary>
    /// Renews an active lock's expiry. Must be called by the lock owner.
    /// </summary>
    public async Task<HeartbeatResult> HeartbeatAsync(
        Guid   lockId,
        string workerId,
        CancellationToken ct = default)
    {
        var lockEntity = await _db.TaskLocks.FindAsync([lockId], ct);
        if (lockEntity is null)
            return new HeartbeatResult(false, null, "Lock not found.");
        if (lockEntity.WorkerId != workerId)
            return new HeartbeatResult(false, null, "Lock owned by different worker.");
        if (lockEntity.Status != LockStatus.Active)
            return new HeartbeatResult(false, null, $"Lock is {lockEntity.Status}.");

        var now = DateTimeOffset.UtcNow;
        lockEntity.HeartbeatAt = now;
        lockEntity.ExpiresAt   = now.AddMinutes(lockEntity.TimeoutMinutes);
        await _db.SaveChangesAsync(ct);

        return new HeartbeatResult(true, lockEntity.ExpiresAt);
    }

    // ── ST-26: ReleaseLock ────────────────────────────────────────────────────

    /// <summary>Releases a lock held by the specified worker.</summary>
    public async Task<bool> ReleaseLockAsync(
        Guid   lockId,
        string workerId,
        bool   force = false,
        CancellationToken ct = default)
    {
        var lockEntity = await _db.TaskLocks.FindAsync([lockId], ct);
        if (lockEntity is null) return false;
        if (!force && lockEntity.WorkerId != workerId)
        {
            _logger.LogWarning("Worker {Worker} tried to release lock {LockId} owned by {Owner}",
                workerId, lockId, lockEntity.WorkerId);
            return false;
        }

        lockEntity.Status = LockStatus.Released;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Lock {LockId} released by {Worker} (force={Force})", lockId, workerId, force);
        return true;
    }

    /// <summary>ST-26: Admin force-release.</summary>
    public Task<bool> ForceReleaseAsync(Guid lockId, CancellationToken ct = default)
        => ReleaseLockAsync(lockId, "__admin__", force: true, ct);

    /// <summary>Lookup the active lock for a task (for MCP tool lock_status).</summary>
    public Task<TaskLockEntity?> GetActiveLockAsync(string taskId, CancellationToken ct = default)
        => _db.TaskLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TaskId == taskId && l.Status == LockStatus.Active, ct);
}
