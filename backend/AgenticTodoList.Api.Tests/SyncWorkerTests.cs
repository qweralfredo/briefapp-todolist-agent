using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BriefappTodoList.Api.Services;
using Xunit;

namespace BriefappTodoList.Api.Tests;

/// <summary>
/// Tests for BL-15: Briefapp API ↔ DevLake Sync Service (DevLakeSyncWorker).
/// Refs: backlog/BL-15 | sprint/SP-12
/// </summary>
public class SyncWorkerTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    // ─── Service registration ─────────────────────────────────────────────

    [Fact]
    public void DevLakeSyncWorker_IsRegistered_AsHostedService()
    {
        var hostedServices = factory.Services.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s is DevLakeSyncWorker);
    }

    [Fact]
    public void DevLakeSyncService_IsRegistered()
    {
        var svc = factory.Services.GetService<DevLakeSyncService>();
        Assert.NotNull(svc);
    }

    // ─── Manual sync trigger ──────────────────────────────────────────────

    [Fact]
    public async Task POST_DevLakeSync_WithoutApiKey_Returns401()
    {
        var response = await factory.CreateClient()
            .PostAsync("/api/devlake/sync", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_DevLakeSync_WithValidApiKey_Returns200()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/devlake/sync");
        request.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("synced", out _));
        Assert.True(body.TryGetProperty("triggeredAt", out _));
    }

    // ─── Sync status endpoint ─────────────────────────────────────────────

    [Fact]
    public async Task GET_DevLakeSyncStatus_WithoutApiKey_Returns401()
    {
        var response = await factory.CreateClient()
            .GetAsync("/api/devlake/sync/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_DevLakeSyncStatus_WithValidApiKey_Returns200()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/devlake/sync/status");
        request.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("isEnabled", out _));
        Assert.True(body.TryGetProperty("syncIntervalMinutes", out _));
    }
}
