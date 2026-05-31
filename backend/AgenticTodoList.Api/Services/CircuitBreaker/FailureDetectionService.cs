using BriefappTodoList.Api.Domain.CircuitBreaker;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.CircuitBreaker;

// ── ST-51: FailureDetectionService ────────────────────────────────────────────

/// <summary>
/// ST-51: Translates NackCategory into FailureCategory and drives the circuit breaker.
/// Integrated into AckService — called on every ACK/NACK.
/// Also detects pattern-based failures (consecutive same-assert test failures).
/// </summary>
public sealed class FailureDetectionService
{
    private readonly ICircuitBreakerService             _breaker;
    private readonly ILogger<FailureDetectionService>   _logger;

    // In-memory rolling pattern tracker (resets on restart — acceptable for prototype)
    private readonly Dictionary<string, List<string>> _recentNackReasons = new();
    private const int PatternWindow = 3;

    public FailureDetectionService(ICircuitBreakerService breaker, ILogger<FailureDetectionService> logger)
    {
        _breaker = breaker;
        _logger  = logger;
    }

    // ── ST-51: ProcessAck ─────────────────────────────────────────────────────

    /// <summary>Called when an agent sends ACK. Resets breaker on success.</summary>
    public async Task ProcessAckAsync(Guid boxId, CancellationToken ct = default)
    {
        ClearPattern(boxId.ToString());
        await _breaker.RecordSuccessAsync(boxId, ct);
    }

    // ── ST-51: ProcessNack ────────────────────────────────────────────────────

    /// <summary>Called when an agent sends NACK. Classifies and records failure.</summary>
    public async Task ProcessNackAsync(
        Guid         boxId,
        NackCategory nackCategory,
        string       reason,
        CancellationToken ct = default)
    {
        var failureCategory = ClassifyFailure(nackCategory, reason);

        // Pattern detection: same failure reason 3x in a row → escalate
        var key    = boxId.ToString();
        var window = TrackPattern(key, reason);
        if (window.Distinct().Count() == 1 && window.Count >= PatternWindow)
        {
            _logger.LogWarning(
                "Repeated failure pattern detected for box {BoxId}: '{Reason}' x{Count}",
                boxId, reason, window.Count);
            failureCategory = failureCategory == FailureCategory.Unknown
                ? FailureCategory.TestFailure  // likely same test assertion
                : failureCategory;
        }

        await _breaker.RecordFailureAsync(boxId, failureCategory, reason, ct);
    }

    // ── ST-51: CheckIsAllowed ─────────────────────────────────────────────────

    /// <summary>Checks if the box is allowed to accept a new task (breaker open check).</summary>
    public Task<bool> IsBoxAllowedAsync(Guid boxId, CancellationToken ct = default) =>
        _breaker.IsAllowedAsync(boxId, ct);

    // ── Classification ────────────────────────────────────────────────────────

    private static FailureCategory ClassifyFailure(NackCategory nack, string reason)
    {
        return nack switch
        {
            NackCategory.CompilationError   => FailureCategory.BuildFailure,
            NackCategory.TestFailure        => FailureCategory.TestFailure,
            NackCategory.ApiTimeout         => FailureCategory.ApiTimeout,
            NackCategory.Hallucination      => FailureCategory.Hallucination,
            NackCategory.ResourceExhaustion => FailureCategory.ResourceExhaustion,
            NackCategory.DependencyError    => FailureCategory.BuildFailure,
            _ => InferFromReason(reason),
        };
    }

    private static FailureCategory InferFromReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return FailureCategory.Unknown;
        var r = reason.ToLowerInvariant();
        if (r.Contains("timeout") || r.Contains("timed out")) return FailureCategory.ApiTimeout;
        if (r.Contains("compile") || r.Contains("build"))     return FailureCategory.BuildFailure;
        if (r.Contains("assert") || r.Contains("test"))       return FailureCategory.TestFailure;
        if (r.Contains("memory") || r.Contains("cpu") ||
            r.Contains("resource") || r.Contains("oom"))      return FailureCategory.ResourceExhaustion;
        if (r.Contains("invalid") || r.Contains("incoherent") ||
            r.Contains("hallucin"))                           return FailureCategory.Hallucination;
        return FailureCategory.Unknown;
    }

    // ── Pattern helpers ───────────────────────────────────────────────────────

    private List<string> TrackPattern(string key, string reason)
    {
        if (!_recentNackReasons.TryGetValue(key, out var window))
        {
            window = new List<string>();
            _recentNackReasons[key] = window;
        }

        window.Add(reason.Length > 80 ? reason[..80] : reason);
        if (window.Count > PatternWindow) window.RemoveAt(0);
        return window;
    }

    private void ClearPattern(string key) => _recentNackReasons.Remove(key);
}
