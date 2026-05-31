using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Budget;
using BriefappTodoList.Api.Services.Budget;

namespace BriefappTodoList.Api.Tests;

public class PromptCacheTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CostGuard_ShouldCalculateCacheSavings_ForAnthropic()
    {
        await using var db = CreateDbContext();
        var guard = new CostGuardService(db, NullLogger<CostGuardService>.Instance);

        // Usage with 1000 tokens used, 500 cached tokens
        var result = await guard.RecordUsageAsync(
            BudgetScope.Box, "agent-box-01",
            tokensUsed: 1000, model: "claude-3-opus", provider: "anthropic", cachedTokens: 500, CancellationToken.None);

        var budget = await guard.GetBudgetAsync(BudgetScope.Box, "agent-box-01", CancellationToken.None);

        Assert.NotNull(budget);
        Assert.Equal(1000, budget.UsedTokens);
        // anthropic discount rate is 0.9. 500 * 0.9 = 450.
        Assert.Equal(450, budget.CachedTokensSaved);
        Assert.Equal(450, result.CachedTokensSaved);
    }

    [Fact]
    public async Task CostGuard_ShouldCalculateCacheSavings_ForOpenAi()
    {
        await using var db = CreateDbContext();
        var guard = new CostGuardService(db, NullLogger<CostGuardService>.Instance);

        await guard.RecordUsageAsync(
            BudgetScope.Box, "openai-box",
            tokensUsed: 100, model: "gpt-4o", provider: "openai", cachedTokens: 1000, CancellationToken.None);

        var budget = await guard.GetBudgetAsync(BudgetScope.Box, "openai-box", CancellationToken.None);

        // openai discount rate is 0.5. 1000 * 0.5 = 500.
        Assert.Equal(500, budget!.CachedTokensSaved);
    }

    [Fact]
    public async Task CostGuard_ShouldCalculateCacheSavings_ForGemini()
    {
        await using var db = CreateDbContext();
        var guard = new CostGuardService(db, NullLogger<CostGuardService>.Instance);

        await guard.RecordUsageAsync(
            BudgetScope.Box, "gemini-box",
            tokensUsed: 200, model: "gemini-1.5", provider: "gemini", cachedTokens: 1000, CancellationToken.None);

        var budget = await guard.GetBudgetAsync(BudgetScope.Box, "gemini-box", CancellationToken.None);

        // gemini discount rate is 0.75. 1000 * 0.75 = 750.
        Assert.Equal(750, budget!.CachedTokensSaved);
    }
}
