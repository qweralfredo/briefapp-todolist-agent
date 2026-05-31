using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.Budget;

// ── ST-75: DTOs ───────────────────────────────────────────────────────────────

public record BudgetCheckResult(
    bool   WithinBudget,
    double UtilizationPercent,
    long   RemainingTokens,
    bool   AlertTriggered,
    bool   HardStopTriggered,
    long   CachedTokensSaved
);

public record BudgetStatsDto(
    Guid         Id,
    BudgetScope  Scope,
    string       ScopeId,
    long         BudgetTokens,
    long         UsedTokens,
    double       UtilizationPercent,
    long         RemainingTokens,
    int          AlertThresholdPercent,
    int          HardStopPercent,
    bool         Frozen,
    bool         AlertTriggered,
    bool         HardStopTriggered,
    long         CachedTokensSaved,
    DateTimeOffset UpdatedAt
);

// ── ST-75: Interface ──────────────────────────────────────────────────────────

public interface ICostGuardService
{
    Task<BudgetCheckResult> RecordUsageAsync(
        BudgetScope scope, string scopeId,
        long tokensUsed, string model, string provider,
        long cachedTokens = 0,
        CancellationToken ct = default);

    Task<TokenBudgetEntity?> GetBudgetAsync(BudgetScope scope, string scopeId, CancellationToken ct = default);
    Task<TokenBudgetEntity>  UpsertBudgetAsync(BudgetScope scope, string scopeId, long budgetTokens, int? alertPercent, int? hardStopPercent, CancellationToken ct = default);
    Task KillSwitchAsync(BudgetScope scope, string scopeId, CancellationToken ct = default);
    Task<List<TokenBudgetEntity>> GetAllBudgetsAsync(CancellationToken ct = default);
}

// ── ST-75: CostGuardService ───────────────────────────────────────────────────

/// <summary>
/// ST-75: Hierarchical token budget tracking.
/// Records usage, checks against alert/hard-stop thresholds, enables kill switch.
/// </summary>
public sealed class CostGuardService : ICostGuardService
{
    private readonly AppDbContext                  _db;
    private readonly ILogger<CostGuardService>    _logger;

    public CostGuardService(AppDbContext db, ILogger<CostGuardService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── RecordUsage ───────────────────────────────────────────────────────────

    public async Task<BudgetCheckResult> RecordUsageAsync(
        BudgetScope scope, string scopeId,
        long tokensUsed, string model, string provider,
        long cachedTokens = 0,
        CancellationToken ct = default)
    {
        var budget = await GetOrCreateBudgetAsync(scope, scopeId, ct);
        budget.UsedTokens += tokensUsed;
        
        // ST-90: Calculate savings (Provider discounts)
        if (cachedTokens > 0)
        {
            var discountRate = provider.ToLowerInvariant() switch
            {
                "anthropic" => 0.9,
                "openai"    => 0.5,
                "gemini"    => 0.75,
                _           => 0.5
            };
            
            budget.CachedTokensSaved += (long)(cachedTokens * discountRate);
        }

        budget.UpdatedAt   = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (budget.HardStopTriggered && !budget.Frozen)
        {
            _logger.LogError(
                "HARD STOP: Budget exhausted for {Scope}/{ScopeId}. Used {Used}/{Budget} tokens.",
                scope, scopeId, budget.UsedTokens, budget.BudgetTokens);

            // Freeze the scope
            budget.Frozen = true;
            await _db.SaveChangesAsync(ct);
        }
        else if (budget.AlertTriggered)
        {
            _logger.LogWarning(
                "BUDGET ALERT: {Scope}/{ScopeId} at {Pct:F1}% ({Used}/{Budget} tokens). Model: {Model}",
                scope, scopeId, budget.UtilizationPercent, budget.UsedTokens, budget.BudgetTokens, model);
        }

        return new BudgetCheckResult(
            WithinBudget:       !budget.HardStopTriggered,
            UtilizationPercent: budget.UtilizationPercent,
            RemainingTokens:    budget.RemainingTokens,
            AlertTriggered:     budget.AlertTriggered,
            HardStopTriggered:  budget.HardStopTriggered,
            CachedTokensSaved:  budget.CachedTokensSaved
        );
    }

    // ── GetBudget ─────────────────────────────────────────────────────────────

    public async Task<TokenBudgetEntity?> GetBudgetAsync(
        BudgetScope scope, string scopeId, CancellationToken ct = default) =>
        await _db.TokenBudgets
            .FirstOrDefaultAsync(b => b.Scope == scope && b.ScopeId == scopeId, ct);

    // ── UpsertBudget ──────────────────────────────────────────────────────────

    public async Task<TokenBudgetEntity> UpsertBudgetAsync(
        BudgetScope scope, string scopeId, long budgetTokens,
        int? alertPercent, int? hardStopPercent, CancellationToken ct = default)
    {
        var budget = await GetOrCreateBudgetAsync(scope, scopeId, ct);
        budget.BudgetTokens           = budgetTokens;
        budget.UpdatedAt              = DateTimeOffset.UtcNow;
        if (alertPercent.HasValue)   budget.AlertThresholdPercent = alertPercent.Value;
        if (hardStopPercent.HasValue) budget.HardStopPercent       = hardStopPercent.Value;
        await _db.SaveChangesAsync(ct);
        return budget;
    }

    // ── KillSwitch ────────────────────────────────────────────────────────────

    public async Task KillSwitchAsync(BudgetScope scope, string scopeId, CancellationToken ct = default)
    {
        var budget   = await GetOrCreateBudgetAsync(scope, scopeId, ct);
        budget.Frozen    = true;
        budget.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogWarning(
            "KILL SWITCH ACTIVATED: {Scope}/{ScopeId} is now frozen by operator.", scope, scopeId);
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    public async Task<List<TokenBudgetEntity>> GetAllBudgetsAsync(CancellationToken ct = default) =>
        await _db.TokenBudgets.AsNoTracking().OrderBy(b => b.Scope).ToListAsync(ct);

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<TokenBudgetEntity> GetOrCreateBudgetAsync(
        BudgetScope scope, string scopeId, CancellationToken ct)
    {
        var budget = await _db.TokenBudgets
            .FirstOrDefaultAsync(b => b.Scope == scope && b.ScopeId == scopeId, ct);

        if (budget is null)
        {
            budget = new TokenBudgetEntity
            {
                Scope   = scope,
                ScopeId = scopeId,
                // Default: unlimited (users must explicitly set a budget)
                BudgetTokens = long.MaxValue,
            };
            _db.TokenBudgets.Add(budget);
            await _db.SaveChangesAsync(ct);
        }

        return budget;
    }
}

// ── ToDto extension ───────────────────────────────────────────────────────────

public static class BudgetExtensions
{
    public static BudgetStatsDto ToDto(this TokenBudgetEntity b) =>
        new(b.Id, b.Scope, b.ScopeId, b.BudgetTokens, b.UsedTokens,
            b.UtilizationPercent, b.RemainingTokens,
            b.AlertThresholdPercent, b.HardStopPercent,
            b.Frozen, b.AlertTriggered, b.HardStopTriggered, b.CachedTokensSaved, b.UpdatedAt);
}
