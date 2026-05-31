using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using Xunit;

namespace BriefappTodoList.Api.Tests;

/// <summary>
/// Tests for BL-07: Token &amp; Cost Analytics Pipeline endpoints.
/// Refs: backlog/BL-07 | sprint/SP-04
/// </summary>
public class TokenAnalyticsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ─── token-summary ───────────────────────────────────────────────────

    [Fact]
    public async Task GET_TokenSummary_NoRuns_Returns200_WithZeroTotals()
    {
        var projectId = Guid.NewGuid(); // project with no agent runs

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/token-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalRuns").GetInt32());
        Assert.Equal(0.0, body.GetProperty("successRate").GetDouble());
        Assert.Equal(0, body.GetProperty("totalTokensInput").GetInt32());
        Assert.Equal(0, body.GetProperty("totalTokensOutput").GetInt32());
    }

    [Fact]
    public async Task GET_TokenSummary_WithRuns_Returns200_WithAggregates()
    {
        var projectId = await SeedProjectAsync();
        await SeedAgentRunsAsync(projectId,
            new AgentRunFixture("gpt-4o",     200,  100, 0.05m, 1000, true),
            new AgentRunFixture("gpt-4o",     300,  150, 0.07m, 1200, true),
            new AgentRunFixture("claude-3-5", 400,  200, 0.09m, 1500, false));

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/token-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("totalRuns").GetInt32());
        // 2/3 success = 66.7%
        Assert.Equal(66.7, body.GetProperty("successRate").GetDouble());
        Assert.Equal(900,  body.GetProperty("totalTokensInput").GetInt32());   // 200+300+400
        Assert.Equal(450,  body.GetProperty("totalTokensOutput").GetInt32());  // 100+150+200
        Assert.Equal(0.21, body.GetProperty("totalCostUsd").GetDouble(), 2);   // 0.05+0.07+0.09
        Assert.True(body.GetProperty("byModel").GetArrayLength() > 0);
        Assert.True(body.GetProperty("dailyRollup").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GET_TokenSummary_ByModel_BreaksDownPerModel()
    {
        var projectId = await SeedProjectAsync();
        await SeedAgentRunsAsync(projectId,
            new AgentRunFixture("model-a", 100, 50, 0.01m, 500, true),
            new AgentRunFixture("model-b", 200, 80, 0.03m, 700, true));

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/token-summary");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var byModel = body.GetProperty("byModel").EnumerateArray().ToList();

        Assert.Equal(2, byModel.Count);
        Assert.Contains(byModel, m => m.GetProperty("model").GetString() == "model-a");
        Assert.Contains(byModel, m => m.GetProperty("model").GetString() == "model-b");
    }

    [Fact]
    public async Task GET_TokenSummary_DailyRollup_GroupsByDate()
    {
        var projectId = await SeedProjectAsync();
        // Seed two runs on "today"
        await SeedAgentRunsAsync(projectId,
            new AgentRunFixture("model-x", 100, 50, 0.01m, 300, true),
            new AgentRunFixture("model-x", 100, 50, 0.02m, 400, true));

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/token-summary?days=7");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rollup = body.GetProperty("dailyRollup").EnumerateArray().ToList();

        Assert.Single(rollup); // both runs are on the same day
        Assert.Equal(2, rollup[0].GetProperty("runs").GetInt32());
    }

    // ─── cost-budget ─────────────────────────────────────────────────────

    [Fact]
    public async Task GET_CostBudget_NoRuns_Returns200_WithZeroSpent()
    {
        var projectId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/cost-budget");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.0, body.GetProperty("spentUsd").GetDouble());
        Assert.Equal("ok", body.GetProperty("alertLevel").GetString());
        Assert.True(body.GetProperty("budgetUsd").GetDouble() > 0);
    }

    [Fact]
    public async Task GET_CostBudget_LowSpend_Returns_AlertLevel_Ok()
    {
        var projectId = await SeedProjectAsync();
        await SeedAgentRunsAsync(projectId,
            new AgentRunFixture("model-x", 100, 50, 1.0m, 300, true)); // $1 of $100 default = 1%

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/cost-budget");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("ok", body.GetProperty("alertLevel").GetString());
        Assert.True(body.GetProperty("usagePct").GetDouble() < 70);
    }

    [Fact]
    public async Task GET_CostBudget_Returns_SpentUsd_And_RemainingUsd()
    {
        var projectId = await SeedProjectAsync();
        await SeedAgentRunsAsync(projectId,
            new AgentRunFixture("model-x", 100, 50, 5.0m, 300, true));

        var response = await _client.GetAsync($"/api/projects/{projectId}/metrics/cost-budget");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var budget = body.GetProperty("budgetUsd").GetDouble();
        var spent  = body.GetProperty("spentUsd").GetDouble();
        var remaining = body.GetProperty("remainingUsd").GetDouble();

        Assert.Equal(5.0, spent, 2);
        Assert.Equal(Math.Round(budget - spent, 4), Math.Round(remaining, 4));
    }

    // ─── helpers ─────────────────────────────────────────────────────────

    private async Task<Guid> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new ProjectEntity { Name = "Token Analytics Test Project" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedAgentRunsAsync(Guid projectId, params AgentRunFixture[] fixtures)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var f in fixtures)
        {
            db.AgentRunLogs.Add(new AgentRunLogEntity
            {
                ProjectId    = projectId,
                AgentName    = "test-agent",
                EntryPoint   = "test",
                ModelName    = f.ModelName,
                TokensInput  = f.TokensInput,
                TokensOutput = f.TokensOutput,
                CostUsd      = f.CostUsd,
                LatencyMs    = f.LatencyMs,
                Success      = f.Success,
                Status       = f.Success ? "completed" : "failed",
                StartedAt    = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    private record AgentRunFixture(
        string ModelName, int TokensInput, int TokensOutput,
        decimal CostUsd, long LatencyMs, bool Success);
}
