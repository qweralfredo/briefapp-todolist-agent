using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Services;

// ST-41: OpenClawClient — C# SDK wrapper for OpenClaw REST API
// Provides methods to send messages and query channel health.
public class OpenClawClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenClawClient> _logger;
    private readonly OpenClawOptions _opts;

    public OpenClawClient(
        IHttpClientFactory factory,
        ILogger<OpenClawClient> logger,
        IConfiguration config)
    {
        _http = factory.CreateClient("openclaw");
        _logger = logger;
        _opts = config.GetSection("OpenClaw").Get<OpenClawOptions>() ?? new OpenClawOptions();
        _http.BaseAddress = new Uri(_opts.BaseUrl);
        _http.DefaultRequestHeaders.Add("X-Admin-Key", _opts.ApiKey);
    }

    /// <summary>Send a message to a recipient on a given channel.</summary>
    public async Task<string> SendMessageAsync(
        ChannelType channel,
        string recipientId,
        string message,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            throw new InvalidOperationException("OpenClaw integration is disabled (OpenClaw:Enabled=false).");

        var payload = new
        {
            channel = channel.ToString().ToLowerInvariant(),
            recipientId,
            message
        };

        for (int attempt = 1; attempt <= _opts.RetryCount; attempt++)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("/api/send", payload, ct);
                resp.EnsureSuccessStatusCode();
                var result = await resp.Content.ReadFromJsonAsync<SendResult>(cancellationToken: ct);
                return result?.DeliveryId ?? Guid.NewGuid().ToString();
            }
            catch (Exception ex) when (attempt < _opts.RetryCount)
            {
                _logger.LogWarning("OpenClaw SendMessage attempt {Attempt}/{Max} failed: {Msg}", attempt, _opts.RetryCount, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        throw new HttpRequestException("OpenClaw SendMessage failed after all retries.");
    }

    /// <summary>Get connection status of a specific channel.</summary>
    public async Task<ChannelStatusDto?> GetChannelStatusAsync(ChannelType channel, CancellationToken ct = default)
    {
        if (!_opts.Enabled) return null;
        try
        {
            return await _http.GetFromJsonAsync<ChannelStatusDto>(
                $"/api/channels/{channel.ToString().ToLowerInvariant()}/status", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("OpenClaw GetChannelStatus failed: {Msg}", ex.Message);
            return null;
        }
    }

    /// <summary>List all configured and connected channels.</summary>
    public async Task<ChannelStatusDto[]> ListConnectedChannelsAsync(CancellationToken ct = default)
    {
        if (!_opts.Enabled) return [];
        try
        {
            return await _http.GetFromJsonAsync<ChannelStatusDto[]>("/api/channels", ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning("OpenClaw ListChannels failed: {Msg}", ex.Message);
            return [];
        }
    }

    private record SendResult(string DeliveryId);
}

// ST-43: Options model for OpenClaw configuration section
public class OpenClawOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "http://localhost:9700";
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 3;
}

// ST-41: DTO for channel status responses
public record ChannelStatusDto(
    string Channel,
    bool Connected,
    DateTimeOffset? LastMessageAt
);
