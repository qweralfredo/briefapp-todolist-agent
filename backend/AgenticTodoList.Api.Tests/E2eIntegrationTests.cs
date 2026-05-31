using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using Xunit;

namespace BriefappTodoList.Api.Tests;

/// <summary>
/// E2E integration tests for the full DevLake integration pipeline (BL-19 SP-14).
/// Covers: agent run → analytics → evaluation → sync → audit.
/// Refs: backlog/BL-19 | sprint/SP-14
/// </summary>
public class E2eIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ─── Full agent run pipeline ──────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_AgentRun_Then_TokenSummary_ReflectsData()
    {
        // 1. Create a project
        var createProject = await _client.PostAsJsonAsync("/api/projects",
            new { name = "E2E Test Project", description = "e2e" });
        Assert.Equal(HttpStatusCode.Created, createProject.StatusCode);
        var project = await createProject.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // 2. Submit an agent run
        var runPayload = new
        {
            agentName = "claude-code", entryPoint = "e2e-test",
            inputSummary = "test input", outputSummary = "test output",
            startedAt = DateTimeOffset.UtcNow,
            status = "completed", modelName = "claude-sonnet-4-6",
            tokensInput = 500, tokensOutput = 200,
            latencyMs = 1500, costUsd = 0.025, success = true,
            environment = "test"
        };
        var addRun = await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", runPayload);
        Assert.Equal(HttpStatusCode.Created, addRun.StatusCode);
        var run = await addRun.Content.ReadFromJsonAsync<JsonElement>();
        var runId = run.GetProperty("id").GetString()!;

        // 3. Check token summary reflects the run
        var summaryResp = await _client.GetAsync($"/api/projects/{projectId}/metrics/token-summary");
        Assert.Equal(HttpStatusCode.OK, summaryResp.StatusCode);
        var summary = await summaryResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, summary.GetProperty("totalRuns").GetInt32());
        Assert.Equal(500, summary.GetProperty("totalTokensInput").GetInt32());
        Assert.Equal(200, summary.GetProperty("totalTokensOutput").GetInt32());
        Assert.Equal(100.0, summary.GetProperty("successRate").GetDouble());

        // 4. Check cost budget
        var budgetResp = await _client.GetAsync($"/api/projects/{projectId}/metrics/cost-budget");
        Assert.Equal(HttpStatusCode.OK, budgetResp.StatusCode);
        var budget = await budgetResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", budget.GetProperty("alertLevel").GetString());

        // 5. Check model performance
        var perfResp = await _client.GetAsync($"/api/projects/{projectId}/metrics/model-performance");
        Assert.Equal(HttpStatusCode.OK, perfResp.StatusCode);
        var perf = await perfResp.Content.ReadFromJsonAsync<JsonElement>();
        var models = perf.GetProperty("models").EnumerateArray().ToList();
        Assert.Single(models);
        Assert.Equal("claude-sonnet-4-6", models[0].GetProperty("model").GetString());
    }

    [Fact]
    public async Task FullPipeline_AgentRun_Then_HumanEval_AppearsinPending()
    {
        // 1. Create project + run
        var projectId = await CreateProjectAsync("Eval E2E");
        var runId = await CreateAgentRunAsync(projectId);

        // 2. Check pending evaluations — run should appear
        var pendingResp = await _client.GetAsync($"/api/projects/{projectId}/evaluations/pending");
        Assert.Equal(HttpStatusCode.OK, pendingResp.StatusCode);
        var pending = await pendingResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(pending.GetArrayLength() >= 1);

        // 3. Submit evaluation
        var evalPayload = new
        {
            reviewerId = "reviewer-e2e", accuracyScore = 4.0f,
            relevanceScore = 4.0f, completenessScore = 3.5f,
            safetyScore = 5.0f, feedbackText = "Looks good",
            requiresEscalation = false, reviewTimeSeconds = 120
        };
        var evalResp = await _client.PostAsJsonAsync($"/api/agent-runs/{runId}/evaluations", evalPayload);
        Assert.Equal(HttpStatusCode.Created, evalResp.StatusCode);
        var eval = await evalResp.Content.ReadFromJsonAsync<JsonElement>();
        // Composite: (4*0.3 + 4*0.25 + 3.5*0.25 + 5*0.2) * 5 = (1.2+1.0+0.875+1.0)*5 = 20.375
        Assert.True(eval.GetProperty("score").GetDouble() > 4.0);

        // 4. After evaluation, run should no longer appear in pending
        var pendingAfter = await _client.GetAsync($"/api/projects/{projectId}/evaluations/pending");
        var pendingList = await pendingAfter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(pendingList.GetArrayLength() == 0 ||
            !pendingList.EnumerateArray().Any(r => r.GetProperty("id").GetString() == runId));
    }

    // ─── Analytics pipeline ───────────────────────────────────────────────

    [Fact]
    public async Task Analytics_MultipleRuns_AllEndpointsConsistent()
    {
        var projectId = await CreateProjectAsync("Analytics E2E");

        // Seed 10 runs: 8 success, 2 failure, mix of models
        for (int i = 0; i < 8; i++)
            await CreateAgentRunAsync(projectId, model: "claude-sonnet", success: true, costUsd: 0.01m);
        for (int i = 0; i < 2; i++)
            await CreateAgentRunAsync(projectId, model: "gpt-4o", success: false, costUsd: 0.05m);

        // token-summary
        var summary = await _client.GetAsync($"/api/projects/{projectId}/metrics/token-summary");
        var s = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10, s.GetProperty("totalRuns").GetInt32());
        Assert.Equal(2, s.GetProperty("byModel").GetArrayLength());

        // model-performance
        var perf = await _client.GetAsync($"/api/projects/{projectId}/metrics/model-performance");
        var p = await perf.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, p.GetProperty("models").GetArrayLength());

        // audit-log (requires API key)
        var auditReq = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/audit-log");
        auditReq.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");
        var audit = await _client.SendAsync(auditReq);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        var a = await audit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10, a.GetProperty("count").GetInt32());
    }

    // ─── Sync pipeline ────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_TriggerManual_Returns200_WithSyncedCount()
    {
        var syncReq = new HttpRequestMessage(HttpMethod.Post, "/api/devlake/sync");
        syncReq.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");

        var resp = await _client.SendAsync(syncReq);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("synced", out _));
        Assert.True(body.TryGetProperty("triggeredAt", out _));
    }

    [Fact]
    public async Task Sync_Status_ReflectsLastSync_After_Manual_Trigger()
    {
        // Trigger a sync
        var syncReq = new HttpRequestMessage(HttpMethod.Post, "/api/devlake/sync");
        syncReq.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");
        await _client.SendAsync(syncReq);

        // Check status
        var statusReq = new HttpRequestMessage(HttpMethod.Get, "/api/devlake/sync/status");
        statusReq.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");
        var resp = await _client.SendAsync(statusReq);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("lastSyncAt").ValueKind);
    }

    // ─── helpers ─────────────────────────────────────────────────────────

    private async Task<string> CreateProjectAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/projects",
            new { name, description = "e2e" });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private async Task<string> CreateAgentRunAsync(
        string projectId, string model = "claude-sonnet-4-6",
        bool success = true, decimal costUsd = 0.01m)
    {
        var resp = await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", new
        {
            agentName = "test-agent", entryPoint = "e2e",
            inputSummary = "in", outputSummary = "out",
            startedAt = DateTimeOffset.UtcNow,
            status = success ? "completed" : "failed",
            modelName = model, tokensInput = 100, tokensOutput = 50,
            latencyMs = 500, costUsd, success, environment = "test"
        });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }
}
