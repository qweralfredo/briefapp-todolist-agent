namespace BriefappTodoList.Api.Contracts;

// ── BOX3-02: Fallback & Rate Limit & Budget request contracts ─────────────────

/// <summary>ST-73: Override RPM for a provider at runtime.</summary>
public record RateLimitOverrideRequest(int NewRpm);

/// <summary>ST-75/76: Create or update a token budget.</summary>
public record UpsertBudgetRequest(
    long BudgetTokens,
    int? AlertThresholdPercent = null,
    int? HardStopPercent       = null
);

/// <summary>ST-75: Record actual token usage for a scope.</summary>
public record RecordUsageRequest(
    long   TokensUsed,
    string Model    = "unknown",
    string Provider = "unknown"
);
