using System.Security.Cryptography;
using System.Text;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.PromptCache;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.PromptCache;

public interface IPromptCacheService
{
    Task<List<PromptCacheEntryEntity>> GetCacheableSegmentsAsync(Guid boxId, CancellationToken ct = default);
    Task<PromptCacheEntryEntity> UpsertSegmentAsync(Guid boxId, PromptSegmentType segmentType, string content, CancellationToken ct = default);
    Task<CachedPromptPayload> BuildCachedPromptAsync(Guid boxId, string taskPrompt, string provider, CancellationToken ct = default);
    Task RecordCacheHitAsync(Guid entryId, CancellationToken ct = default);
    Task RecordCacheMissAsync(Guid entryId, CancellationToken ct = default);
    Task InvalidateCacheAsync(Guid boxId, PromptSegmentType? segmentType = null, CancellationToken ct = default);
    Task WarmCacheAsync(Guid boxId, CancellationToken ct = default);
    Task<CacheStatsDto> GetCacheStatsAsync(Guid? boxId = null, CancellationToken ct = default);
}

public sealed class PromptCacheService : IPromptCacheService
{
    private readonly AppDbContext                  _db;
    private readonly PromptCacheConfig             _config;
    private readonly ILogger<PromptCacheService>   _logger;

    public PromptCacheService(AppDbContext db, PromptCacheConfig config, ILogger<PromptCacheService> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
    }

    public async Task<List<PromptCacheEntryEntity>> GetCacheableSegmentsAsync(Guid boxId, CancellationToken ct = default)
    {
        return await _db.PromptCacheEntries
            .Where(x => x.BoxId == boxId)
            .OrderBy(x => x.SegmentType)
            .ToListAsync(ct);
    }

    public async Task<PromptCacheEntryEntity> UpsertSegmentAsync(Guid boxId, PromptSegmentType segmentType, string content, CancellationToken ct = default)
    {
        var hash       = ComputeSha256(content);
        var tokenCount = content.Length / 4; // Basic heuristic

        var existing = await _db.PromptCacheEntries
            .FirstOrDefaultAsync(x => x.BoxId == boxId && x.SegmentType == segmentType, ct);

        if (existing != null)
        {
            if (existing.ContentHash == hash)
                return existing; // no visual changes

            // Invalidate/delete old and create new to keep unique hash clean, or just update it
            _db.PromptCacheEntries.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        // Limit check
        var count = await _db.PromptCacheEntries.CountAsync(x => x.BoxId == boxId, ct);
        if (count >= _config.MaxSegmentsPerBox)
        {
            _logger.LogWarning("Box {BoxId} reached max caching segments ({Max}).", boxId, _config.MaxSegmentsPerBox);
            // Delete oldest unused
            var oldest = await _db.PromptCacheEntries
                .Where(x => x.BoxId == boxId)
                .OrderBy(x => x.LastUsedAt)
                .FirstOrDefaultAsync(ct);
            if (oldest != null)
            {
                _db.PromptCacheEntries.Remove(oldest);
            }
        }

        var entry = new PromptCacheEntryEntity
        {
            BoxId       = boxId,
            SegmentType = segmentType,
            ContentHash = hash,
            Content     = content,
            TokenCount  = tokenCount,
            TtlMinutes  = _config.DefaultTtlMinutes,
            CreatedAt   = DateTimeOffset.UtcNow,
            LastUsedAt  = DateTimeOffset.UtcNow
        };

        _db.PromptCacheEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Upserted cache segment {Type} for Box {BoxId} ({Tokens} tokens).", segmentType, boxId, tokenCount);
        return entry;
    }

    public async Task<CachedPromptPayload> BuildCachedPromptAsync(Guid boxId, string taskPrompt, string provider, CancellationToken ct = default)
    {
        var segments = await GetCacheableSegmentsAsync(boxId, ct);
        
        long totalTokens = segments.Sum(s => s.TokenCount) + (taskPrompt.Length / 4);
        long cachedTokens = segments.Sum(s => s.TokenCount);

        double savings = totalTokens > 0 ? (double)cachedTokens / totalTokens * 100 : 0;

        // Note: Actual caching is up to the Provider adapters. This just structures the context.
        return new CachedPromptPayload(segments, taskPrompt, savings);
    }

    public async Task RecordCacheHitAsync(Guid entryId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"PromptCacheEntries\" SET \"HitCount\" = \"HitCount\" + 1, \"LastUsedAt\" = {0} WHERE \"Id\" = {1}",
            DateTimeOffset.UtcNow, entryId);
    }

    public async Task RecordCacheMissAsync(Guid entryId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"PromptCacheEntries\" SET \"MissCount\" = \"MissCount\" + 1, \"LastUsedAt\" = {0} WHERE \"Id\" = {1}",
            DateTimeOffset.UtcNow, entryId);
    }

    public async Task InvalidateCacheAsync(Guid boxId, PromptSegmentType? segmentType = null, CancellationToken ct = default)
    {
        var query = _db.PromptCacheEntries.Where(x => x.BoxId == boxId);
        if (segmentType.HasValue)
            query = query.Where(x => x.SegmentType == segmentType.Value);

        var entries = await query.ToListAsync(ct);
        if (entries.Any())
        {
            _db.PromptCacheEntries.RemoveRange(entries);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Invalidated {Count} cache segments for Box {BoxId}.", entries.Count, boxId);
        }
    }

    public async Task WarmCacheAsync(Guid boxId, CancellationToken ct = default)
    {
        // For warmup, inject a default system prompt if none exists.
        var existingSystem = await _db.PromptCacheEntries
            .FirstOrDefaultAsync(x => x.BoxId == boxId && x.SegmentType == PromptSegmentType.SystemPrompt, ct);

        if (existingSystem == null)
        {
            var defaultSystem = "You are an autonomous AI agent running in a sandbox. You must fulfill the user's task directly and completely, outputting accurate results. Think carefully before taking action, and prioritize security and system constraints. You can run python code to accomplish the task.";
            await UpsertSegmentAsync(boxId, PromptSegmentType.SystemPrompt, defaultSystem, ct);
        }
        
        _logger.LogInformation("Warmup completed for box {BoxId}", boxId);
    }

    public async Task<CacheStatsDto> GetCacheStatsAsync(Guid? boxId = null, CancellationToken ct = default)
    {
        var query = _db.PromptCacheEntries.AsQueryable();
        if (boxId.HasValue)
            query = query.Where(x => x.BoxId == boxId.Value);

        var stats = await query
            .GroupBy(x => 1)
            .Select(g => new
            {
                Hits   = g.Sum(x => x.HitCount),
                Misses = g.Sum(x => x.MissCount),
                Tokens = g.Sum(x => (long)x.TokenCount),
                Count  = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        if (stats == null) return new CacheStatsDto(0, 0, 0, 0, 0);

        long totalReqs   = stats.Hits + stats.Misses;
        double hitRate   = totalReqs > 0 ? (double)stats.Hits / totalReqs * 100 : 0;

        return new CacheStatsDto(stats.Hits, stats.Misses, hitRate, stats.Tokens, stats.Count);
    }

    private static string ComputeSha256(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash  = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
