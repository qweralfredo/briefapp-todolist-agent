using System.Text.Json;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

// ── Types ──────────────────────────────────────────────────────────────────────

/// <summary>ST-15: Result of a Tansu publish operation.</summary>
public record TansuPublishResult(string MessageId, string Topic, DateTimeOffset PublishedAt);

/// <summary>ST-15: Health status of the Tansu.io service.</summary>
public record TansuHealthResult(bool Healthy, string Status, string? Version = null);

/// <summary>ST-15: Consumer lag info for a topic.</summary>
public record TansuTopicStats(string Topic, long Pending, long Processing, long Completed);

// ── Interface ──────────────────────────────────────────────────────────────────

/// <summary>
/// ST-15: Abstraction over the Tansu.io message broker HTTP API.
/// Enables dependency-injection and mocking in unit tests.
/// </summary>
public interface ITansuClient
{
    Task<TansuPublishResult> PublishAsync(string topic, object payload, TimeSpan? delay = null, CancellationToken ct = default);
    Task<TansuHealthResult>  HealthCheckAsync(CancellationToken ct = default);
    Task<TansuTopicStats[]>  GetTopicStatsAsync(string? prefix = null, CancellationToken ct = default);
}

// ── Implementation ─────────────────────────────────────────────────────────────

/// <summary>
/// ST-15: HTTP client wrapper for Tansu.io REST API.
/// Tansu.io is a lightweight Kafka-compatible message broker.
/// Base URL: TANSU_HOST:TANSU_PORT (default localhost:9600).
/// </summary>
public sealed class TansuClient : ITansuClient
{
    private readonly HttpClient _http;
    private readonly ILogger<TansuClient> _logger;

    public TansuClient(IHttpClientFactory factory, ILogger<TansuClient> logger)
    {
        _http   = factory.CreateClient("tansu");
        _logger = logger;
    }

    /// <summary>Publishes a task payload to a Tansu topic.</summary>
    public async Task<TansuPublishResult> PublishAsync(
        string topic,
        object payload,
        TimeSpan? delay = null,
        CancellationToken ct = default)
    {
        var body = new
        {
            topic,
            payload = JsonSerializer.Serialize(payload),
            scheduled_after_ms = delay.HasValue ? (long)delay.Value.TotalMilliseconds : (long?)null,
        };

        var response = await _http.PostAsJsonAsync("/api/messages", body, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TansuPublishResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from Tansu.");

        _logger.LogDebug("Published to {Topic}: messageId={MessageId}", topic, result.id);
        return new TansuPublishResult(result.id, topic, DateTimeOffset.UtcNow);
    }

    public async Task<TansuHealthResult> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", ct);
            if (!response.IsSuccessStatusCode)
                return new TansuHealthResult(false, $"HTTP {response.StatusCode}");

            var data = await response.Content.ReadFromJsonAsync<TansuHealthResponse>(cancellationToken: ct);
            return new TansuHealthResult(true, data?.status ?? "ok", data?.version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Tansu health check failed: {Msg}", ex.Message);
            return new TansuHealthResult(false, ex.Message);
        }
    }

    public async Task<TansuTopicStats[]> GetTopicStatsAsync(string? prefix = null, CancellationToken ct = default)
    {
        var url = prefix is not null ? $"/api/topics?prefix={Uri.EscapeDataString(prefix)}" : "/api/topics";
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return [];

        var data = await response.Content.ReadFromJsonAsync<TansuTopicsResponse>(cancellationToken: ct);
        return data?.topics?.Select(t => new TansuTopicStats(t.name, t.pending, t.processing, t.completed)).ToArray()
            ?? [];
    }

    // ── Internal deserialization types ────────────────────────────────────────
    private record TansuPublishResponse(string id);
    private record TansuHealthResponse(string status, string? version);
    private record TansuTopicInfo(string name, long pending, long processing, long completed);
    private record TansuTopicsResponse(TansuTopicInfo[]? topics);
}
