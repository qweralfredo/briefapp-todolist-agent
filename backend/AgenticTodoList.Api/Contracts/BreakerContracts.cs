namespace BriefappTodoList.Api.Contracts;

// ── ST-52: Circuit Breaker config request ────────────────────────────────────

/// <summary>
/// ST-52: Request body for POST /api/breaker/{boxId}/config
/// Updates per-Box circuit breaker configuration. All fields are optional.
/// </summary>
public record UpdateBreakerConfigRequest(
    int? FailureThreshold,
    int? CooldownSeconds,
    int? HalfOpenMaxCalls
);
