using BriefappTodoList.Api.Domain.PromptCache;

namespace BriefappTodoList.Api.Services.PromptCache;

/// <summary>
/// Contains the payload broken down into cacheable and dynamic parts.
/// </summary>
public record CachedPromptPayload(
    List<PromptCacheEntryEntity> CacheableSegments,
    string                       DynamicPrompt,
    double                       EstimatedSavingsPercent
);

/// <summary>
/// Status statistics of the prompt cache.
/// </summary>
public record CacheStatsDto(
    long   TotalHits,
    long   TotalMisses,
    double HitRatePercent,
    long   TotalTokensCached,
    int    SegmentCount
);

public record UpsertSegmentRequest(
    string Content
);
