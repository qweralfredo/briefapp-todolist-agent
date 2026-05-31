namespace BriefappTodoList.Api.Services.PromptCache;

public class PromptCacheConfig
{
    /// <summary>
    /// Default TTL for explicit cache systems like Gemini Context Caching.
    /// </summary>
    public int DefaultTtlMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum cache segments per box to prevent unlimited growth.
    /// </summary>
    public int MaxSegmentsPerBox { get; set; } = 10;

    /// <summary>
    /// Thresholds for provider caching to activate. 
    /// If token count is below this, the adapter might bypass cache.
    /// </summary>
    public Dictionary<string, int> MinCacheTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "anthropic", 1024 },
        { "openai",    1024 },
        { "gemini",    32768 }
    };
}
