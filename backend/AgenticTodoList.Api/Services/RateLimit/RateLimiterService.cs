using System.Collections.Concurrent;

namespace BriefappTodoList.Api.Services.RateLimit;

// ── ST-71: Provider config ────────────────────────────────────────────────────

public record ProviderRateConfig(string Provider, int MaxRpm);

// ── ST-71: RateLimiterService ─────────────────────────────────────────────────

/// <summary>
/// ST-71: Token Bucket rate limiter per LLM provider.
/// Uses in-process counters (Redis-backed refill optional via env config).
/// </summary>
public sealed class RateLimiterService : IDisposable
{
    // Bucket state per provider
    private sealed class Bucket
    {
        public int            MaxRpm   { get; set; }
        public double         Tokens   { get; set; }
        public DateTimeOffset LastRefill { get; set; } = DateTimeOffset.UtcNow;
        public SemaphoreSlim  Lock     { get; } = new(1, 1);
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets  = new();
    private readonly ILogger<RateLimiterService>          _logger;

    // Default RPM limits per provider (overridable via env: RATELIMIT_{PROVIDER}_RPM)
    private static readonly Dictionary<string, int> DefaultLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"]    = 60,
        ["gemini"]    = 360,
        ["anthropic"] = 60,
    };

    public RateLimiterService(ILogger<RateLimiterService> logger)
    {
        _logger = logger;
        foreach (var (provider, rpm) in DefaultLimits)
        {
            var envKey = $"RATELIMIT_{provider.ToUpperInvariant()}_RPM";
            var envVal = Environment.GetEnvironmentVariable(envKey);
            var limit  = int.TryParse(envVal, out var v) ? v : rpm;
            _buckets[provider] = new Bucket { MaxRpm = limit, Tokens = limit };
        }
    }

    // ── ST-71: TryAcquire ─────────────────────────────────────────────────────

    /// <summary>ST-71: Tries to consume 1 token. Returns false if rate limit exceeded.</summary>
    public async Task<bool> TryAcquireAsync(string provider, CancellationToken ct = default)
    {
        var bucket = GetOrCreate(provider);
        await bucket.Lock.WaitAsync(ct);
        try
        {
            Refill(bucket);
            if (bucket.Tokens < 1)
            {
                _logger.LogDebug("Rate limit exceeded for provider {Provider}.", provider);
                return false;
            }
            bucket.Tokens--;
            return true;
        }
        finally { bucket.Lock.Release(); }
    }

    // ── ST-71: WaitForToken ───────────────────────────────────────────────────

    /// <summary>ST-71: Waits until a token is available (async block).</summary>
    public async Task WaitForTokenAsync(string provider, CancellationToken ct = default)
    {
        while (true)
        {
            if (await TryAcquireAsync(provider, ct)) return;
            var delayMs = GetRefillDelayMs(provider);
            await Task.Delay(delayMs, ct);
        }
    }

    // ── ST-72: GetCurrentRpm ──────────────────────────────────────────────────

    /// <summary>ST-72: Returns current tracked RPM (tokens consumed in last minute).</summary>
    public int GetCurrentRpm(string provider)
    {
        var bucket = GetOrCreate(provider);
        return bucket.MaxRpm - (int)Math.Max(0, bucket.Tokens);
    }

    // ── ST-72: GetUtilizationPercent ──────────────────────────────────────────

    /// <summary>ST-72: Returns provider utilization as percentage.</summary>
    public double GetUtilizationPercent(string provider)
    {
        var bucket = GetOrCreate(provider);
        return bucket.MaxRpm == 0 ? 0 : (double)GetCurrentRpm(provider) / bucket.MaxRpm * 100;
    }

    // ── Admin override ─────────────────────────────────────────────────────────

    /// <summary>Temporarily overrides RPM for a provider (1h TTL in real impl).</summary>
    public void OverrideRpm(string provider, int newRpm)
    {
        var bucket   = GetOrCreate(provider);
        bucket.MaxRpm = newRpm;
        _logger.LogWarning("Rate limit override: {Provider} → {Rpm} RPM", provider, newRpm);
    }

    /// <summary>Returns all configured provider limits.</summary>
    public IReadOnlyDictionary<string, int> GetAllLimits() =>
        _buckets.ToDictionary(kv => kv.Key, kv => kv.Value.MaxRpm);

    // ── Private helpers ───────────────────────────────────────────────────────

    private Bucket GetOrCreate(string provider)
    {
        provider = provider.ToLowerInvariant();
        return _buckets.GetOrAdd(provider, _ => new Bucket
        {
            MaxRpm = DefaultLimits.TryGetValue(provider, out var lim) ? lim : 60,
            Tokens = DefaultLimits.TryGetValue(provider, out var lim2) ? lim2 : 60,
        });
    }

    private static void Refill(Bucket bucket)
    {
        var now     = DateTimeOffset.UtcNow;
        var elapsed = (now - bucket.LastRefill).TotalSeconds;
        // Tokens refill at MaxRpm/60 per second
        var refill  = elapsed * (bucket.MaxRpm / 60.0);
        bucket.Tokens      = Math.Min(bucket.MaxRpm, bucket.Tokens + refill);
        bucket.LastRefill  = now;
    }

    private int GetRefillDelayMs(string provider)
    {
        var bucket = GetOrCreate(provider);
        return bucket.MaxRpm == 0 ? 1000 : (int)(60_000.0 / bucket.MaxRpm) + 50;
    }

    public void Dispose()
    {
        foreach (var b in _buckets.Values) b.Lock.Dispose();
    }
}
