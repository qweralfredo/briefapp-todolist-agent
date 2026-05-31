using BriefappTodoList.Api.Domain.Fallback;
using BriefappTodoList.Api.Services.Queue;

namespace BriefappTodoList.Api.Services.Fallback;

// ── ST-68: Interface ──────────────────────────────────────────────────────────

/// <summary>Context passed to each fallback strategy.</summary>
public record FallbackContext(
    Guid   BoxId,
    string TaskId,
    string OriginalModel,
    string OriginalPrompt,
    string FailureReason,
    int    AttemptNumber
);

/// <summary>Result returned by a fallback strategy.</summary>
public record FallbackResult(
    bool    Success,
    string? NewModel       = null,
    string? NewTaskPayload = null,
    string  Message        = ""
);

/// <summary>ST-68: Contract every fallback strategy must implement.</summary>
public interface IFallbackStrategy
{
    FallbackStrategyType Type { get; }
    Task<FallbackResult> ExecuteAsync(FallbackContext ctx, CancellationToken ct = default);
}

// ── ST-68: RetryWithLowerTemperature ─────────────────────────────────────────

/// <summary>ST-68: Retries with temperature lowered from 0.7 → 0.3.</summary>
public sealed class RetryWithLowerTemperatureStrategy : IFallbackStrategy
{
    public FallbackStrategyType Type => FallbackStrategyType.RetryWithLowerTemperature;

    public Task<FallbackResult> ExecuteAsync(FallbackContext ctx, CancellationToken ct = default)
    {
        // Inject temperature override into payload
        var newPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            original_task_id = ctx.TaskId,
            prompt           = ctx.OriginalPrompt,
            model            = ctx.OriginalModel,
            temperature      = 0.3,
            fallback_attempt = ctx.AttemptNumber,
        });

        return Task.FromResult(new FallbackResult(
            Success:        true,
            NewModel:       ctx.OriginalModel,
            NewTaskPayload: newPayload,
            Message:        $"Retrying task {ctx.TaskId} with lower temperature (0.3)"
        ));
    }
}

// ── ST-68: SwapToLargerModel ──────────────────────────────────────────────────

/// <summary>ST-68: Swaps to a more capable model (haiku→sonnet→opus).</summary>
public sealed class SwapToLargerModelStrategy : IFallbackStrategy
{
    // Ordered from smallest → largest
    private static readonly string[] ModelChain =
    [
        "claude-haiku",
        "claude-sonnet",
        "claude-opus",
        "gpt-4o-mini",
        "gpt-4o",
    ];

    public FallbackStrategyType Type => FallbackStrategyType.SwapToLargerModel;

    public Task<FallbackResult> ExecuteAsync(FallbackContext ctx, CancellationToken ct = default)
    {
        var currentIdx = Array.FindIndex(ModelChain,
            m => ctx.OriginalModel.Contains(m.Split('-').Last(), StringComparison.OrdinalIgnoreCase));

        if (currentIdx < 0 || currentIdx >= ModelChain.Length - 1)
            return Task.FromResult(new FallbackResult(false, Message: "No larger model available."));

        var newModel   = ModelChain[currentIdx + 1];
        var newPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            original_task_id = ctx.TaskId,
            prompt           = ctx.OriginalPrompt,
            model            = newModel,
            fallback_attempt = ctx.AttemptNumber,
        });

        return Task.FromResult(new FallbackResult(
            Success:        true,
            NewModel:       newModel,
            NewTaskPayload: newPayload,
            Message:        $"Swapped from {ctx.OriginalModel} to larger model {newModel}"
        ));
    }
}

// ── ST-68: SwapToSmallerModel ─────────────────────────────────────────────────

/// <summary>ST-68: Swaps to a cheaper, smaller model (opus→sonnet→haiku).</summary>
public sealed class SwapToSmallerModelStrategy : IFallbackStrategy
{
    private static readonly string[] ModelChain =
    [
        "claude-haiku",
        "claude-sonnet",
        "claude-opus",
        "gpt-4o-mini",
        "gpt-4o",
    ];

    public FallbackStrategyType Type => FallbackStrategyType.SwapToSmallerModel;

    public Task<FallbackResult> ExecuteAsync(FallbackContext ctx, CancellationToken ct = default)
    {
        var currentIdx = Array.FindIndex(ModelChain,
            m => ctx.OriginalModel.Contains(m.Split('-').Last(), StringComparison.OrdinalIgnoreCase));

        if (currentIdx <= 0)
            return Task.FromResult(new FallbackResult(false, Message: "Already using smallest model."));

        var newModel   = ModelChain[currentIdx - 1];
        var newPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            original_task_id = ctx.TaskId,
            prompt           = ctx.OriginalPrompt,
            model            = newModel,
            fallback_attempt = ctx.AttemptNumber,
        });

        return Task.FromResult(new FallbackResult(
            Success:        true,
            NewModel:       newModel,
            NewTaskPayload: newPayload,
            Message:        $"Swapped from {ctx.OriginalModel} to smaller model {newModel} (cost saving)"
        ));
    }
}

// ── ST-68: SimplifyPrompt ─────────────────────────────────────────────────────

/// <summary>ST-68: Truncates the prompt to 50% of original length.</summary>
public sealed class SimplifyPromptStrategy : IFallbackStrategy
{
    public FallbackStrategyType Type => FallbackStrategyType.SimplifyPrompt;

    public Task<FallbackResult> ExecuteAsync(FallbackContext ctx, CancellationToken ct = default)
    {
        var simplified    = ctx.OriginalPrompt.Length > 100
            ? ctx.OriginalPrompt[..(ctx.OriginalPrompt.Length / 2)] + "\n\n[Context truncated for retry]"
            : ctx.OriginalPrompt;

        var newPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            original_task_id = ctx.TaskId,
            prompt           = simplified,
            model            = ctx.OriginalModel,
            fallback_attempt = ctx.AttemptNumber,
        });

        return Task.FromResult(new FallbackResult(
            Success:        true,
            NewModel:       ctx.OriginalModel,
            NewTaskPayload: newPayload,
            Message:        $"Simplified prompt to {simplified.Length} chars (was {ctx.OriginalPrompt.Length})"
        ));
    }
}

// ── ST-68: NotifyHuman ────────────────────────────────────────────────────────

/// <summary>ST-68: Notifies a human operator via OpenClaw and marks the task blocked.</summary>
public sealed class NotifyHumanStrategy : IFallbackStrategy
{
    private readonly ILogger<NotifyHumanStrategy> _logger;

    public NotifyHumanStrategy(ILogger<NotifyHumanStrategy> logger) => _logger = logger;

    public FallbackStrategyType Type => FallbackStrategyType.NotifyHuman;

    public Task<FallbackResult> ExecuteAsync(FallbackContext ctx, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[HUMAN REQUIRED] Box {BoxId}, Task {TaskId} requires human intervention. Reason: {Reason}",
            ctx.BoxId, ctx.TaskId, ctx.FailureReason);

        // In production: publish alert via OpenClawClient or Tansu briefapp.system.alerts
        // For now: log warning (OpenClaw integration in SP-BOX4-02)

        return Task.FromResult(new FallbackResult(
            Success: false, // marks the chain as exhausted → task goes Blocked
            Message: $"Human notification sent for box {ctx.BoxId}, task {ctx.TaskId}. Reason: {ctx.FailureReason}"
        ));
    }
}
