using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BriefappTodoList.Api.Domain.PromptCache;

/// <summary>
/// Types of prompt segments that can be cached.
/// </summary>
public enum PromptSegmentType
{
    SystemPrompt    = 0,
    ToolDefinitions = 1,
    ProjectContext  = 2,
    CustomPrefix    = 3
}

/// <summary>
/// ST-87: Stores a cacheable segment of a prompt for a specific Box.
/// </summary>
public class PromptCacheEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Box this cache entry belongs to.
    /// </summary>
    public Guid BoxId { get; set; }

    /// <summary>
    /// Logical type of this segment (e.g. system prompt, tools).
    /// </summary>
    public PromptSegmentType SegmentType { get; set; }

    /// <summary>
    /// SHA-256 hash of the content to quickly check for mutations.
    /// </summary>
    [MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// The actual text content to send to the provider.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Estimated or actual number of tokens.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// How many times this specific exact cache entry was used/hit.
    /// </summary>
    public long HitCount { get; set; } = 0;

    /// <summary>
    /// How many times the entry missed or was bypassed (optional stat).
    /// </summary>
    public long MissCount { get; set; } = 0;

    /// <summary>
    /// Configure time-to-live if the provider supports explicit TTL (e.g. Gemini).
    /// Default is 60 minutes.
    /// </summary>
    public int TtlMinutes { get; set; } = 60;

    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
}
