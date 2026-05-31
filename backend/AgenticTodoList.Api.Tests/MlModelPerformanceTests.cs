using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using Xunit;

namespace BriefappTodoList.Api.Tests;

/// <summary>
/// Tests for BL-17: ML Model Performance Tracking endpoints.
/// Refs: backlog/BL-17 | sprint/SP-13
/// </summary>
public class MlModelPerformanceTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    // ─── /api/projects/{id}/metrics/model-performance ────────────────────

    [Fact]
    public async Task GET_ModelPerformance_NoRuns_Returns200_WithEmptyModels()
    {
        var projectId = Guid.NewGuid();

        var response = await factory.CreateClient()
            .GetAsync($"/api/projects/{projectId}/metrics/model-performance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("models").GetArrayLength());
    }

    [Fact]
    public async Task GET_ModelPerformance_WithRuns_Returns_LatencyPercentiles()
    {
        var projectId = await SeedProjectAsync();
        // Seed 5 runs for the same model with known latencies
        await SeedRunsAsync(projectId, "claude-sonnet",
            latencies: [100, 200, 300, 400, 500]);

        var response = await factory.CreateClient()
            .GetAsync($"/api/projects/{projectId}/metrics/model-performance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var models = body.GetProperty("models").EnumerateArray().ToList();

        Assert.Single(models);
        var m = models[0];
        Assert.Equal("claude-sonnet", m.GetProperty("model").GetString());
        Assert.Equal(5, m.GetProperty("totalRuns").GetInt32());
        // p50 of [100,200,300,400,500] = 300
        Assert.Equal(300, m.GetProperty("p50LatencyMs").GetInt64());
        // p95 ≥ 400
        Assert.True(m.GetProperty("p95LatencyMs").GetInt64() >= 400);
    }

    [Fact]
    public async Task GET_ModelPerformance_MultipleModels_ReturnsLeaderboard()
    {
        var projectId = await SeedProjectAsync();
        await SeedRunsAsync(projectId, "model-a", latencies: [100, 150], successRate: 1.0);
        await SeedRunsAsync(projectId, "model-b", latencies: [500, 600], successRate: 0.5);

        var response = await factory.CreateClient()
            .GetAsync($"/api/projects/{projectId}/metrics/model-performance");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var models = body.GetProperty("models").EnumerateArray().ToList();

        Assert.Equal(2, models.Count);
        // model-a has lower latency → should appear first (leaderboard sorted by p50 asc)
        Assert.Equal("model-a", models[0].GetProperty("model").GetString());
    }

    // ─── /api/projects/{id}/metrics/drift ────────────────────────────────

    [Fact]
    public async Task GET_DriftReport_NoRuns_Returns200_WithNoAlerts()
    {
        var projectId = Guid.NewGuid();

        var response = await factory.CreateClient()
            .GetAsync($"/api/projects/{projectId}/metrics/drift");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("alerts").GetArrayLength());
    }

    [Fact]
    public async Task GET_DriftReport_WithDegradedModel_Returns_Alert()
    {
        var projectId = await SeedProjectAsync();

        // Recent runs: high latency (simulating degradation)
        await SeedRunsWithDateAsync(projectId, "degraded-model",
            latencyMs: 2000, daysAgo: 1, count: 5);
        // Old baseline: low latency (must be within 7-day query window but before recent 3-day window)
        await SeedRunsWithDateAsync(projectId, "degraded-model",
            latencyMs: 500, daysAgo: 5, count: 5);

        var response = await factory.CreateClient()
            .GetAsync($"/api/projects/{projectId}/metrics/drift");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var alerts = body.GetProperty("alerts").EnumerateArray().ToList();

        Assert.NotEmpty(alerts);
        var alert = alerts[0];
        Assert.Equal("degraded-model", alert.GetProperty("model").GetString());
        Assert.True(alert.GetProperty("driftPct").GetDouble() > 15.0); // >15% drift
    }

    // ─── helpers ─────────────────────────────────────────────────────────

    private async Task<Guid> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = new ProjectEntity { Name = "ML Perf Test" };
        db.Projects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task SeedRunsAsync(Guid projectId, string model, long[] latencies, double successRate = 1.0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (int i = 0; i < latencies.Length; i++)
        {
            db.AgentRunLogs.Add(new AgentRunLogEntity
            {
                ProjectId = projectId, AgentName = "test", ModelName = model,
                LatencyMs = latencies[i], Success = i < (int)(latencies.Length * successRate),
                Status = "completed", StartedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedRunsWithDateAsync(Guid projectId, string model, long latencyMs, int daysAgo, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var startedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        for (int i = 0; i < count; i++)
        {
            db.AgentRunLogs.Add(new AgentRunLogEntity
            {
                ProjectId = projectId, AgentName = "test", ModelName = model,
                LatencyMs = latencyMs, Success = true,
                Status = "completed", StartedAt = startedAt
            });
        }
        await db.SaveChangesAsync();
    }
}
