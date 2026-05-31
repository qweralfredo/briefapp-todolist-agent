using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using BriefappTodoList.Api.Services;
using Xunit;

namespace BriefappTodoList.Api.Tests;

/// <summary>
/// RED tests for BL-13 (SSE /api/metrics/stream) and BL-16 (/api/devlake/webhook).
/// Refs: backlog/c175101c | backlog/33eaee36 | sprint/77da6071
/// </summary>
public class RealtimeEndpointsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ─── BL-13: SSE endpoint ──────────────────────────────────────────────

    [Fact]
    public async Task GET_MetricsStream_Returns200_WithTextEventStreamContentType()
    {
        // Arrange — SSE requests should never wait; use a timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/metrics/stream");
        request.Headers.Add("Accept", "text/event-stream");

        // Act
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GET_MetricsStream_Returns_KeepAliveEvents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/metrics/stream");
        request.Headers.Add("Accept", "text/event-stream");

        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Read first chunk — should contain ": keep-alive" or "event: connected"
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        var firstLine = await reader.ReadLineAsync(cts.Token) ?? string.Empty;

        Assert.True(
            firstLine.StartsWith(":") || firstLine.StartsWith("event:") || firstLine.StartsWith("data:"),
            $"Expected SSE format, got: '{firstLine}'");
    }

    // ─── BL-13: MetricsEventService ──────────────────────────────────────

    [Fact]
    public void MetricsEventService_IsRegistered_AsSingleton()
    {
        using var scope = factory.Services.CreateScope();
        var svc1 = factory.Services.GetRequiredService<MetricsEventService>();
        var svc2 = factory.Services.GetRequiredService<MetricsEventService>();
        Assert.Same(svc1, svc2);
    }

    [Fact]
    public async Task MetricsEventService_Publish_DeliversEvent_ToSubscriber()
    {
        var svc = factory.Services.GetRequiredService<MetricsEventService>();
        MetricsEvent? received = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var reader = svc.Subscribe();
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in reader.ReadAllAsync(cts.Token))
            {
                received = evt;
                break;
            }
        }, cts.Token);

        svc.Publish(new MetricsEvent("agent_run_completed", new { runId = "abc" }));
        await readTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotNull(received);
        Assert.Equal("agent_run_completed", received!.EventType);
    }

    // ─── BL-16: Webhook endpoint ─────────────────────────────────────────

    [Fact]
    public async Task POST_DevLakeWebhook_WithoutSignature_Returns401()
    {
        var payload = JsonSerializer.Serialize(new { eventType = "agent_run_completed", data = new { } });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/devlake/webhook", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_DevLakeWebhook_WithInvalidSignature_Returns401()
    {
        // Need a fresh factory with a secret so HMAC verification is active
        await using var f = new TestAppFactory();
        f.WithWebhookSecret("test-secret-for-invalid-sig");
        var client = f.CreateClient();

        var payload = JsonSerializer.Serialize(new { eventType = "agent_run_completed", data = new { } });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/devlake/webhook") { Content = content };
        request.Headers.Add("X-Briefapp-Signature-256", "sha256=invalidsignature");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_DevLakeWebhook_WithValidHmac_Returns200()
    {
        var secret = "briefapp-webhook-secret-test";
        factory.WithWebhookSecret(secret);

        var payload = JsonSerializer.Serialize(new
        {
            eventType = "agent_run_completed",
            data = new { runId = Guid.NewGuid(), success = true }
        });
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hmac = ComputeHmacSha256(payloadBytes, secret);

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/devlake/webhook") { Content = content };
        request.Headers.Add("X-Briefapp-Signature-256", $"sha256={hmac}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_DevLakeWebhook_WithUnknownEvent_Returns422()
    {
        var secret = "briefapp-webhook-secret-test";
        factory.WithWebhookSecret(secret);

        var payload = JsonSerializer.Serialize(new { eventType = "unknown_event_xyz", data = new { } });
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hmac = ComputeHmacSha256(payloadBytes, secret);

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/devlake/webhook") { Content = content };
        request.Headers.Add("X-Briefapp-Signature-256", $"sha256={hmac}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static string ComputeHmacSha256(byte[] payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }
}
