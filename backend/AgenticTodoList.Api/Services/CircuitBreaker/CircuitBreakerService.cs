using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.CircuitBreaker;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.CircuitBreaker;

// ── Interface ─────────────────────────────────────────────────────────────────

public interface ICircuitBreakerService
{
    Task<bool> IsAllowedAsync(Guid boxId, CancellationToken ct = default);
    Task RecordSuccessAsync(Guid boxId, CancellationToken ct = default);
    Task RecordFailureAsync(Guid boxId, FailureCategory category, string reason = "", CancellationToken ct = default);
    Task<CircuitBreakerEntity> GetOrCreateAsync(Guid boxId, CancellationToken ct = default);
    Task ResetAsync(Guid boxId, CancellationToken ct = default);
    Task UpdateConfigAsync(Guid boxId, int? threshold, int? cooldown, int? halfOpenMax, CancellationToken ct = default);
    Task<List<BreakerTransitionEntity>> GetHistoryAsync(Guid boxId, int limit, CancellationToken ct = default);
    Task<List<CircuitBreakerEntity>> GetAllAsync(CancellationToken ct = default);
}

// ── DTO ───────────────────────────────────────────────────────────────────────

public record CircuitBreakerDto(
    Guid                BoxId,
    CircuitBreakerState State,
    int                 FailureCount,
    int                 FailureThreshold,
    int                 CooldownSeconds,
    int                 HalfOpenMaxCalls,
    int                 HalfOpenCallCount,
    DateTimeOffset?     TrippedAt,
    DateTimeOffset?     LastFailureAt,
    DateTimeOffset      LastTransitionAt,
    bool                CooldownExpired,
    string              StateLabel
);

// ── ST-50: CircuitBreakerService ─────────────────────────────────────────────

/// <summary>
/// ST-50: Circuit Breaker FSM with PostgreSQL persistence.
/// Thread-safe via DB-level optimistic concurrency row version.
///
/// Transitions:
///   Closed  → Open      : FailureCount >= FailureThreshold
///   Open    → HalfOpen  : Cooldown expired + IsAllowed called
///   HalfOpen → Closed   : RecordSuccess
///   HalfOpen → Open     : RecordFailure
/// </summary>
public sealed class CircuitBreakerService : ICircuitBreakerService
{
    private readonly AppDbContext                      _db;
    private readonly ILogger<CircuitBreakerService>   _logger;

    public CircuitBreakerService(AppDbContext db, ILogger<CircuitBreakerService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── ST-50: GetOrCreate ────────────────────────────────────────────────────

    public async Task<CircuitBreakerEntity> GetOrCreateAsync(Guid boxId, CancellationToken ct = default)
    {
        var breaker = await _db.CircuitBreakers
            .FirstOrDefaultAsync(b => b.BoxId == boxId, ct);

        if (breaker is null)
        {
            breaker = new CircuitBreakerEntity { BoxId = boxId };
            _db.CircuitBreakers.Add(breaker);
            await _db.SaveChangesAsync(ct);
        }

        return breaker;
    }

    // ── ST-50: IsAllowed ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the breaker allows a request through.
    /// Side-effects: may transition Open → HalfOpen if cooldown expired.
    /// </summary>
    public async Task<bool> IsAllowedAsync(Guid boxId, CancellationToken ct = default)
    {
        var b = await GetOrCreateAsync(boxId, ct);

        switch (b.State)
        {
            case CircuitBreakerState.Closed:
                return true;

            case CircuitBreakerState.Open:
                if (b.CooldownExpired)
                {
                    await TransitionAsync(b, CircuitBreakerState.HalfOpen,
                        null, "Cooldown expired — entering probe window.", ct);
                    return true; // allow first probe
                }
                _logger.LogDebug("Circuit breaker OPEN for box {BoxId} — request rejected.", boxId);
                return false;

            case CircuitBreakerState.HalfOpen:
                if (b.HalfOpenCallCount < b.HalfOpenMaxCalls)
                {
                    b.HalfOpenCallCount++;
                    await _db.SaveChangesAsync(ct);
                    return true;
                }
                _logger.LogDebug("Circuit breaker HALF-OPEN probe quota exhausted for box {BoxId}.", boxId);
                return false;

            default:
                return true;
        }
    }

    // ── ST-50: RecordSuccess ──────────────────────────────────────────────────

    public async Task RecordSuccessAsync(Guid boxId, CancellationToken ct = default)
    {
        var b = await GetOrCreateAsync(boxId, ct);
        if (b.State == CircuitBreakerState.HalfOpen)
        {
            await TransitionAsync(b, CircuitBreakerState.Closed,
                null, "Probe succeeded — circuit reset to Closed.", ct);
            _logger.LogInformation("Circuit breaker CLOSED (reset) for box {BoxId}.", boxId);
        }
        else if (b.State == CircuitBreakerState.Closed && b.FailureCount > 0)
        {
            b.FailureCount = 0;
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── ST-50: RecordFailure ──────────────────────────────────────────────────

    public async Task RecordFailureAsync(Guid boxId, FailureCategory category,
        string reason = "", CancellationToken ct = default)
    {
        var b = await GetOrCreateAsync(boxId, ct);
        b.FailureCount++;
        b.LastFailureAt = DateTimeOffset.UtcNow;

        var fullReason = $"[{category}] {reason}";

        if (b.FailureCount >= b.FailureThreshold && b.State != CircuitBreakerState.Open)
        {
            await TransitionAsync(b, CircuitBreakerState.Open, category, fullReason, ct);
            _logger.LogWarning(
                "Circuit breaker TRIPPED (Open) for box {BoxId}: {Failures}/{Threshold} failures. Reason: {Reason}",
                boxId, b.FailureCount, b.FailureThreshold, fullReason);
        }
        else if (b.State == CircuitBreakerState.HalfOpen)
        {
            // Probe failed → reopen immediately
            await TransitionAsync(b, CircuitBreakerState.Open, category,
                $"Probe failed — re-opening. {fullReason}", ct);
            _logger.LogWarning("Circuit breaker probe FAILED — re-opened for box {BoxId}.", boxId);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── ST-50: Reset ──────────────────────────────────────────────────────────

    public async Task ResetAsync(Guid boxId, CancellationToken ct = default)
    {
        var b = await GetOrCreateAsync(boxId, ct);
        await TransitionAsync(b, CircuitBreakerState.Closed, null, "Manual reset by operator.", ct);
        _logger.LogWarning("Circuit breaker manually RESET to Closed for box {BoxId}.", boxId);
    }

    // ── ST-50: UpdateConfig ───────────────────────────────────────────────────

    public async Task UpdateConfigAsync(
        Guid boxId, int? threshold, int? cooldown, int? halfOpenMax,
        CancellationToken ct = default)
    {
        var b = await GetOrCreateAsync(boxId, ct);
        if (threshold.HasValue && threshold > 0) b.FailureThreshold = threshold.Value;
        if (cooldown.HasValue  && cooldown  > 0) b.CooldownSeconds  = cooldown.Value;
        if (halfOpenMax.HasValue && halfOpenMax > 0) b.HalfOpenMaxCalls = halfOpenMax.Value;
        await _db.SaveChangesAsync(ct);
    }

    // ── ST-50: GetHistory ─────────────────────────────────────────────────────

    public async Task<List<BreakerTransitionEntity>> GetHistoryAsync(
        Guid boxId, int limit = 50, CancellationToken ct = default) =>
        await _db.BreakerTransitions
            .Where(t => t.BoxId == boxId)
            .OrderByDescending(t => t.TriggeredAt)
            .Take(limit)
            .ToListAsync(ct);

    // ── ST-52: GetAll ─────────────────────────────────────────────────────────

    public async Task<List<CircuitBreakerEntity>> GetAllAsync(CancellationToken ct = default) =>
        await _db.CircuitBreakers.AsNoTracking().ToListAsync(ct);

    // ── Private: TransitionAsync ──────────────────────────────────────────────

    private async Task TransitionAsync(
        CircuitBreakerEntity cb,
        CircuitBreakerState  newState,
        FailureCategory?     category,
        string               reason,
        CancellationToken    ct)
    {
        var transition = new BreakerTransitionEntity
        {
            BoxId     = cb.BoxId,
            FromState = cb.State,
            ToState   = newState,
            Category  = category,
            Reason    = reason,
        };

        var from = cb.State;
        cb.State             = newState;
        cb.LastTransitionAt  = DateTimeOffset.UtcNow;
        cb.HalfOpenCallCount = 0; // always reset probe count on any transition

        if (newState == CircuitBreakerState.Open)
        {
            cb.TrippedAt = DateTimeOffset.UtcNow;
        }
        else if (newState == CircuitBreakerState.Closed)
        {
            cb.FailureCount  = 0;
            cb.TrippedAt     = null;
            cb.LastFailureAt = null;
        }

        _db.BreakerTransitions.Add(transition);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Circuit breaker {BoxId}: {From} → {To}. {Reason}",
            cb.BoxId, from, newState, reason);
    }
}

// ── Helper: ToDto ─────────────────────────────────────────────────────────────

public static class CircuitBreakerExtensions
{
    public static CircuitBreakerDto ToDto(this CircuitBreakerEntity cb) =>
        new(cb.BoxId, cb.State, cb.FailureCount, cb.FailureThreshold,
            cb.CooldownSeconds, cb.HalfOpenMaxCalls, cb.HalfOpenCallCount,
            cb.TrippedAt, cb.LastFailureAt, cb.LastTransitionAt,
            cb.CooldownExpired, cb.State.ToString());
}
