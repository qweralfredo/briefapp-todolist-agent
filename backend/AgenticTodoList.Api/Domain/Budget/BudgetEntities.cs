namespace BriefappTodoList.Api.Domain.Budget;

// ── ST-74: Enums ──────────────────────────────────────────────────────────────

/// <summary>ST-74: Scope level in the budget hierarchy.</summary>
public enum BudgetScope
{
    Platform = 0,
    Box      = 1,
    Sprint   = 2,
    Task     = 3,
}

// ── ST-74: TokenBudgetEntity ──────────────────────────────────────────────────

/// <summary>
/// ST-74: Hierarchical token budget.
/// Platform → Box → Sprint → Task (each level constrained by parent).
/// </summary>
public class TokenBudgetEntity
{
    public Guid          Id                   { get; set; } = Guid.NewGuid();
    public BudgetScope   Scope                { get; set; }

    /// <summary>ID of the scoped entity (project/box/sprint/task id as string).</summary>
    public string        ScopeId              { get; set; } = string.Empty;

    public long          BudgetTokens         { get; set; }
    public long          UsedTokens           { get; set; }
    public long          CachedTokensSaved    { get; set; }

    /// <summary>Percentage of budget at which an alert is triggered. Default: 80.</summary>
    public int           AlertThresholdPercent { get; set; } = 80;

    /// <summary>Percentage at which a hard stop is applied. Default: 100.</summary>
    public int           HardStopPercent      { get; set; } = 100;

    /// <summary>If true, any new task submission is frozen (emergency kill switch).</summary>
    public bool          Frozen               { get; set; } = false;

    public DateTimeOffset CreatedAt           { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt           { get; set; } = DateTimeOffset.UtcNow;

    // ── Computed helpers ───────────────────────────────────────────────────────
    public double UtilizationPercent =>
        BudgetTokens == 0 ? 0 : (double)UsedTokens / BudgetTokens * 100;

    public long RemainingTokens => Math.Max(0, BudgetTokens - UsedTokens);

    public bool AlertTriggered =>
        BudgetTokens > 0 && UtilizationPercent >= AlertThresholdPercent;

    public bool HardStopTriggered =>
        Frozen || (BudgetTokens > 0 && UtilizationPercent >= HardStopPercent);
}
