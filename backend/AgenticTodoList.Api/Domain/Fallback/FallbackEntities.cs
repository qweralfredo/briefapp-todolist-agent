namespace BriefappTodoList.Api.Domain.Fallback;

// ── ST-68: Enums ─────────────────────────────────────────────────────────────

/// <summary>ST-68: Names of available fallback strategies.</summary>
public enum FallbackStrategyType
{
    RetryWithLowerTemperature = 0,
    SwapToLargerModel         = 1,
    SwapToSmallerModel        = 2,
    SimplifyPrompt            = 3,
    NotifyHuman               = 4,
}

// ── ST-70: FallbackAttemptLog ─────────────────────────────────────────────────

/// <summary>ST-70: Immutable audit record of each fallback strategy attempt.</summary>
public class FallbackAttemptLog
{
    public Guid               Id           { get; set; } = Guid.NewGuid();
    public Guid               BoxId        { get; set; }
    public string             TaskId       { get; set; } = string.Empty;
    public FallbackStrategyType Strategy   { get; set; }
    public bool               Success      { get; set; }
    public int                DurationMs   { get; set; }
    public string?            FromModel    { get; set; }
    public string?            ToModel      { get; set; }
    public string             Message      { get; set; } = string.Empty;
    public DateTimeOffset     Timestamp    { get; set; } = DateTimeOffset.UtcNow;
}
