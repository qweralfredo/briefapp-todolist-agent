using BriefappTodoList.Api.Domain.PromptCache;

namespace BriefappTodoList.Api.Services.PromptCache.Adapters;

/// <summary>
/// Output payload prepared for a specific provider.
/// </summary>
public record ProviderCachedPayload(
    List<object>               FormattedSystemBlocks,
    List<object>               FormattedMessages,
    Dictionary<string, string> HttpHeaders,
    string                     ProviderName,
    bool                       IsCacheActive
);

/// <summary>
/// Strategy to format cacheable segments into provider-specific API structures.
/// </summary>
public interface IProviderCacheAdapter
{
    string ProviderName { get; }
    ProviderCachedPayload ApplyCacheControl(List<PromptCacheEntryEntity> segments, string dynamicPrompt);
}

// ── Anthropic ─────────────────────────────────────────────────────────────────
public class AnthropicCacheAdapter : IProviderCacheAdapter
{
    public string ProviderName => "anthropic";

    public ProviderCachedPayload ApplyCacheControl(List<PromptCacheEntryEntity> segments, string dynamicPrompt)
    {
        var systemBlocks = new List<object>();

        // Anthropic supports cache_control on up to 4 blocks in the request.
        // We add cache_control: {"type": "ephemeral"} on the largest stable blocks.
        foreach (var seg in segments.OrderByDescending(s => s.TokenCount))
        {
            var block = new Dictionary<string, object>
            {
                { "type", "text" },
                { "text", seg.Content }
            };

            // Anthropic min cache prefix is 1024 tokens.
            if (seg.TokenCount >= 1024 && systemBlocks.Count(b => b is Dictionary<string,object> d && d.ContainsKey("cache_control")) < 3)
            {
                block["cache_control"] = new { type = "ephemeral" };
            }

            systemBlocks.Add(block);
        }

        var messages = new List<object>
        {
            new { role = "user", content = dynamicPrompt }
        };

        var headers = new Dictionary<string, string>
        {
            { "anthropic-beta", "prompt-caching-2024-07-31" }
        };

        return new ProviderCachedPayload(
            FormattedSystemBlocks: systemBlocks,
            FormattedMessages: messages,
            HttpHeaders: headers,
            ProviderName: ProviderName,
            IsCacheActive: systemBlocks.Count > 0
        );
    }
}

// ── OpenAI ────────────────────────────────────────────────────────────────────
public class OpenAiCacheAdapter : IProviderCacheAdapter
{
    public string ProviderName => "openai";

    public ProviderCachedPayload ApplyCacheControl(List<PromptCacheEntryEntity> segments, string dynamicPrompt)
    {
        // OpenAI uses automatic prefix caching. We just need to ensure determinism.
        // Sort stably: System -> Tools -> Context
        var orderedSegments = segments.OrderBy(s => (int)s.SegmentType).ToList();
        
        var messages = new List<object>();

        foreach (var seg in orderedSegments)
        {
            messages.Add(new { role = "system", content = seg.Content });
        }
        
        messages.Add(new { role = "user", content = dynamicPrompt });

        return new ProviderCachedPayload(
            FormattedSystemBlocks: new List<object>(), // OpenAI treats system differently, all in messages often
            FormattedMessages: messages,
            HttpHeaders: new Dictionary<string, string>(), // No special headers needed
            ProviderName: ProviderName,
            IsCacheActive: orderedSegments.Sum(s => s.TokenCount) >= 1024
        );
    }
}

// ── Gemini ────────────────────────────────────────────────────────────────────
public class GeminiCacheAdapter : IProviderCacheAdapter
{
    public string ProviderName => "gemini";

    public ProviderCachedPayload ApplyCacheControl(List<PromptCacheEntryEntity> segments, string dynamicPrompt)
    {
        var messages = new List<object>();
        var totalTokens = segments.Sum(s => s.TokenCount);
        
        // Gemini Context Caching requires >= 32768 tokens.
        // Note: For < 32K, we just pass as normal context without Context Caching API.
        bool useExplicitCache = totalTokens >= 32768;

        if (!useExplicitCache)
        {
            // Fallback: concatenate
            var combined = string.Join("\n\n---\n\n", segments.Select(s => s.Content));
            messages.Add(new { role = "user", parts = new[] { new { text = combined + "\n\n" + dynamicPrompt } } });
        }
        else
        {
            // Placeholder for Gemini explicit cache name reference behavior.
            // When using Context Caching API, the request needs `cachedContent: "cacheName"`
            // Since we are formatting the payload here, we mock the structure:
            var headers = new Dictionary<string, string>
            {
                { "X-Gemini-Cache-Intent", "true" } // Pseudo-header to let gateway know to use CachedContents.create
            };

            messages.Add(new { role = "user", parts = new[] { new { text = dynamicPrompt } } });

            return new ProviderCachedPayload(
                FormattedSystemBlocks: segments.Select(s => (object)new { text = s.Content }).ToList(),
                FormattedMessages: messages,
                HttpHeaders: headers,
                ProviderName: ProviderName,
                IsCacheActive: true
            );
        }

        return new ProviderCachedPayload(
            FormattedSystemBlocks: new List<object>(),
            FormattedMessages: messages,
            HttpHeaders: new Dictionary<string, string>(),
            ProviderName: ProviderName,
            IsCacheActive: false
        );
    }
}

// ── ProviderCacheAdapterFactory ───────────────────────────────────────────────
public class ProviderCacheAdapterFactory
{
    private readonly IEnumerable<IProviderCacheAdapter> _adapters;

    public ProviderCacheAdapterFactory(IEnumerable<IProviderCacheAdapter> adapters)
    {
        _adapters = adapters;
    }

    public IProviderCacheAdapter? GetAdapter(string provider)
    {
        return _adapters.FirstOrDefault(a => a.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }
}
