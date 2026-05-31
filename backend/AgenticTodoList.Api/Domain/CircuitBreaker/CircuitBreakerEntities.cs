namespace BriefappTodoList.Api.Domain.CircuitBreaker;

// ── ST-49: Enums ──────────────────────────────────────────────────────────────

/// <summary>ST-49: FSM states for the Circuit Breaker.</summary>
public enum CircuitBreakerState
{
    /// <summary>Normal operation — all requests pass through.</summary>
    Closed   = 0,

    /// <summary>Tripped — all requests are rejected immediately.</summary>
    Open     = 1,

    /// <summary>Cooldown expired — allows exactly HalfOpenMaxCalls probe request(s).</summary>
    HalfOpen = 2,
}

/// <summary>ST-51: Category of failure that contributed to tripping the breaker.</summary>
public enum FailureCategory
{
    BuildFailure        = 0,
    TestFailure         = 1,
    ApiTimeout          = 2,
    Hallucination       = 3,
    ResourceExhaustion  = 4,
    Unknown             = 99,
}

// ── ST-49: CircuitBreakerEntity ───────────────────────────────────────────────

/// <summary>
/// ST-49: Persisted state of a per-Box circuit breaker.
/// Unique index on BoxId — one breaker per Box.
/// </summary>
public class CircuitBreakerEntity
{
    public Guid   Id    { get; set; } = Guid.NewGuid();

    /// <summary>Box this breaker belongs to.</summary>
    public Guid   BoxId { get; set; }

    // ── State ──────────────────────────────────────────────────────────────────
    public CircuitBreakerState State         { get; set; } = CircuitBreakerState.Closed;
    public int                 FailureCount  { get; set; } = 0;
    public DateTimeOffset?     LastFailureAt { get; set; }
    public DateTimeOffset?     TrippedAt     { get; set; }
    public int                 HalfOpenCallCount { get; set; } = 0;
    public DateTimeOffset      LastTransitionAt  { get; set; } = DateTimeOffset.UtcNow;

    // ── Config (per-Box, embedded) ─────────────────────────────────────────────
    /// <summary>Consecutive failures before tripping. Default: 3.</summary>
    public int FailureThreshold  { get; set; } = 3;

    /// <summary>Seconds to stay Open before probing. Default: 300 (5 min).</summary>
    public int CooldownSeconds   { get; set; } = 300;

    /// <summary>Max probe requests in Half-Open before deciding. Default: 1.</summary>
    public int HalfOpenMaxCalls  { get; set; } = 1;

    // ── Helpers ────────────────────────────────────────────────────────────────
    public bool CooldownExpired =>
        TrippedAt.HasValue &&
        DateTimeOffset.UtcNow >= TrippedAt.Value.AddSeconds(CooldownSeconds);
}

// ── ST-49: BreakerTransitionEntity ────────────────────────────────────────────

/// <summary>ST-49: Immutable audit record of each FSM state transition.</summary>
public class BreakerTransitionEntity
{
    public Guid                Id          { get; set; } = Guid.NewGuid();
    public Guid                BoxId       { get; set; }
    public CircuitBreakerState FromState   { get; set; }
    public CircuitBreakerState ToState     { get; set; }
    public FailureCategory?    Category    { get; set; }
    public string              Reason      { get; set; } = string.Empty;
    public DateTimeOffset      TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
}
