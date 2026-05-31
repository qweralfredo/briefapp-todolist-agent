using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Fallback;
using BriefappTodoList.Api.Services.CircuitBreaker;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.Fallback;

// ── ST-69: FallbackChainExecutor ──────────────────────────────────────────────

/// <summary>
/// ST-69: Executes a sequential chain of fallback strategies for a failed task.
/// Default chain: RetryLowerTemp → SwapLarger → SwapSmaller → SimplifyPrompt → NotifyHuman.
/// Stops at first Success. If all fail, NotifyHuman is forced at the end.
/// Also integrates with CircuitBreaker: if circuit trips → chain executes automatically.
/// </summary>
public sealed class FallbackChainExecutor
{
    private readonly IEnumerable<IFallbackStrategy>   _strategies;
    private readonly AppDbContext                      _db;
    private readonly ILogger<FallbackChainExecutor>   _logger;

    // Default ordered chain
    private static readonly FallbackStrategyType[] DefaultChain =
    [
        FallbackStrategyType.RetryWithLowerTemperature,
        FallbackStrategyType.SwapToLargerModel,
        FallbackStrategyType.SwapToSmallerModel,
        FallbackStrategyType.SimplifyPrompt,
        FallbackStrategyType.NotifyHuman,
    ];

    public FallbackChainExecutor(
        IEnumerable<IFallbackStrategy> strategies,
        AppDbContext                   db,
        ILogger<FallbackChainExecutor> logger)
    {
        _strategies = strategies;
        _db         = db;
        _logger     = logger;
    }

    // ── ST-69: Execute ────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the fallback chain in order.
    /// Returns the first successful FallbackResult, or the NotifyHuman result.
    /// Also persists FallbackAttemptLog entries for each attempt.
    /// </summary>
    public async Task<FallbackResult> ExecuteAsync(
        FallbackContext         ctx,
        FallbackStrategyType[]? chain = null,
        CancellationToken       ct    = default)
    {
        chain ??= DefaultChain;

        _logger.LogWarning(
            "Fallback chain started for Box {BoxId} Task {TaskId}. Failure: {Reason}",
            ctx.BoxId, ctx.TaskId, ctx.FailureReason);

        FallbackResult? lastResult = null;

        for (int i = 0; i < chain.Length; i++)
        {
            var strategyType = chain[i];
            var strategy     = _strategies.FirstOrDefault(s => s.Type == strategyType);

            if (strategy is null)
            {
                _logger.LogWarning("Fallback strategy {Type} not registered — skipping.", strategyType);
                continue;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var attempt = ctx with { AttemptNumber = i + 1 };
                lastResult  = await strategy.ExecuteAsync(attempt, ct);
                sw.Stop();

                await PersistAttemptAsync(ctx, strategyType, lastResult, (int)sw.ElapsedMilliseconds, ct);

                _logger.LogInformation(
                    "Fallback [{Type}] for {TaskId}: {Success} — {Message}",
                    strategyType, ctx.TaskId, lastResult.Success ? "SUCCESS" : "FAILED", lastResult.Message);

                if (lastResult.Success) return lastResult;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Fallback strategy {Type} threw an exception.", strategyType);

                var errorResult = new FallbackResult(false, Message: ex.Message);
                await PersistAttemptAsync(ctx, strategyType, errorResult, (int)sw.ElapsedMilliseconds, ct);
            }
        }

        // If NotifyHuman was not included in chain, force it
        if (!chain.Contains(FallbackStrategyType.NotifyHuman))
        {
            var notify = _strategies.FirstOrDefault(s => s.Type == FallbackStrategyType.NotifyHuman);
            if (notify is not null)
                lastResult = await notify.ExecuteAsync(ctx with { AttemptNumber = chain.Length + 1 }, ct);
        }

        return lastResult ?? new FallbackResult(false, Message: "All fallback strategies exhausted.");
    }

    // ── ST-70: PersistAttempt ─────────────────────────────────────────────────

    private async Task PersistAttemptAsync(
        FallbackContext     ctx,
        FallbackStrategyType strategyType,
        FallbackResult      result,
        int                 durationMs,
        CancellationToken   ct)
    {
        _db.FallbackAttemptLogs.Add(new FallbackAttemptLog
        {
            BoxId      = ctx.BoxId,
            TaskId     = ctx.TaskId,
            Strategy   = strategyType,
            Success    = result.Success,
            DurationMs = durationMs,
            FromModel  = ctx.OriginalModel,
            ToModel    = result.NewModel,
            Message    = result.Message,
        });

        await _db.SaveChangesAsync(ct);
    }
}
