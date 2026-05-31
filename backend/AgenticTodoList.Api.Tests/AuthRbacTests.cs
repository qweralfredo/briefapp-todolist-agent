using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using BriefappTodoList.Api.Services;
using Xunit;

namespace BriefappTodoList.Api.Tests;

/// <summary>
/// RED tests for BL-14: DevLake Authentication &amp; RBAC.
/// API Key validation service + optional key-protected endpoints.
/// Refs: backlog/BL-14 | sprint/SP-11
/// </summary>
public class AuthRbacTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    // ─── ApiKeyService unit tests ────────────────────────────────────────

    [Fact]
    public void ApiKeyService_IsRegistered_AsSingleton()
    {
        var svc1 = factory.Services.GetRequiredService<ApiKeyService>();
        var svc2 = factory.Services.GetRequiredService<ApiKeyService>();
        Assert.Same(svc1, svc2);
    }

    [Fact]
    public void ApiKeyService_ValidKey_ReturnsTrue()
    {
        var svc = factory.Services.GetRequiredService<ApiKeyService>();
        // The test factory injects a known key "test-api-key-1234"
        Assert.True(svc.IsValid("test-api-key-1234"));
    }

    [Fact]
    public void ApiKeyService_InvalidKey_ReturnsFalse()
    {
        var svc = factory.Services.GetRequiredService<ApiKeyService>();
        Assert.False(svc.IsValid("completely-wrong-key"));
    }

    [Fact]
    public void ApiKeyService_EmptyKey_ReturnsFalse()
    {
        var svc = factory.Services.GetRequiredService<ApiKeyService>();
        Assert.False(svc.IsValid(string.Empty));
    }

    [Fact]
    public void ApiKeyService_NullKey_ReturnsFalse()
    {
        var svc = factory.Services.GetRequiredService<ApiKeyService>();
        Assert.False(svc.IsValid(null));
    }

    // ─── Protected endpoint tests ────────────────────────────────────────

    [Fact]
    public async Task GET_AgentRunAuditLog_WithoutApiKey_Returns401()
    {
        var projectId = Guid.NewGuid();
        var response = await factory.CreateClient()
            .GetAsync($"/api/projects/{projectId}/audit-log");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_AgentRunAuditLog_WithInvalidApiKey_Returns401()
    {
        var projectId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/audit-log");
        request.Headers.Add("X-Briefapp-Api-Key", "wrong-key");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_AgentRunAuditLog_WithValidApiKey_Returns200()
    {
        var projectId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/audit-log");
        request.Headers.Add("X-Briefapp-Api-Key", "test-api-key-1234");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
