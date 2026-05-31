using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BriefappTodoList.Api.Services.Fallback;

/// <summary>
/// ST-70: Ollama Local Service for fallback.
/// Connects to a local embedded Ollama instance to inference when the primary model fails.
/// </summary>
public class OllamaLocalService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLocalService> _logger;

    public OllamaLocalService(HttpClient httpClient, ILogger<OllamaLocalService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> GenerateAsync(string prompt, string model = "llama3", CancellationToken ct = default)
    {
        _logger.LogInformation("Attempting inference with embedded Ollama (Model: {Model})...", model);

        var payload = new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = false
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            // Alterado para /api/chat conforme requisito da task d52c713b-f339-4229-8700-28facb05b031
            var response = await _httpClient.PostAsync("/api/chat", content, ct);
            response.EnsureSuccessStatusCode();

            var jsonStream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<OllamaChatResponse>(jsonStream, cancellationToken: ct);

            return result?.Message?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach local Ollama embedded fallback.");
            return null; // The ultimate failure
        }
    }
}

public class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
    
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }
    
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
