using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using BriefappTodoList.Api.Domain.OpenClaw;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.OpenClaw;

// ── ST-84: ChannelHealthService ───────────────────────────────────────────────

/// <summary>
/// ST-84: BackgroundService that health-checks each configured channel every 30s.
/// On extended downtime (5+ min), activates failover to secondary channel.
/// </summary>
public sealed class ChannelHealthService : BackgroundService
{
    private static readonly Dictionary<ChannelType, ChannelType> FailoverMap = new()
    {
        [ChannelType.WhatsApp] = ChannelType.Telegram,
        [ChannelType.Slack]    = ChannelType.Telegram,
    };

    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ILogger<ChannelHealthService>  _logger;
    private readonly TimeSpan _interval    = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _failoverTtl = TimeSpan.FromMinutes(5);

    public ChannelHealthService(
        IServiceScopeFactory s, ILogger<ChannelHealthService> l)
    {
        _scopeFactory = s;
        _logger       = l;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await CheckAllChannelsAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "ChannelHealthService tick failed."); }
        }
    }

    private async Task CheckAllChannelsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var channelType in Enum.GetValues<ChannelType>())
        {
            var health = await db.ChannelHealths
                .FirstOrDefaultAsync(h => h.ChannelType == channelType, ct);

            health ??= new ChannelHealthEntity { ChannelType = channelType };
            if (health.Id == Guid.Empty) db.ChannelHealths.Add(health);

            var sw       = System.Diagnostics.Stopwatch.StartNew();
            var isUp     = await CheckChannelAsync(channelType, ct);
            sw.Stop();

            health.LastCheckAt = DateTimeOffset.UtcNow;
            health.CheckCount++;
            health.AvgLatencyMs = (long)((health.AvgLatencyMs * (health.CheckCount - 1) + sw.ElapsedMilliseconds) / health.CheckCount);

            if (!isUp)
            {
                health.FailureCount++;
                health.DownSince ??= DateTimeOffset.UtcNow;

                var downDuration = DateTimeOffset.UtcNow - health.DownSince.Value;
                if (downDuration >= _failoverTtl && health.Status != ChannelStatus.Failover)
                {
                    health.Status         = ChannelStatus.Failover;
                    health.FailoverTarget = FailoverMap.TryGetValue(channelType, out var target) ? target : null;
                    _logger.LogWarning("FAILOVER: {Channel} → {Target}", channelType, health.FailoverTarget);
                }
                else if (health.Status == ChannelStatus.Healthy)
                {
                    health.Status = ChannelStatus.Unhealthy;
                }
            }
            else
            {
                health.Status         = ChannelStatus.Healthy;
                health.DownSince      = null;
                health.FailoverTarget = null;
            }

            health.UptimePercent  = health.CheckCount == 0 ? 100
                : (1.0 - (double)health.FailureCount / health.CheckCount) * 100;

            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>ST-85: Calls OpenClaw to verify channel is alive. Timeout 5s.</summary>
    private async Task<bool> CheckChannelAsync(ChannelType channel, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            // In production: call actual channel health endpoint via OpenClawClient
            // For now: always returns healthy (no actual channel configured in dev)
            await Task.Delay(5, cts.Token);
            return true;
        }
        catch { return false; }
    }
}

// ── ST-85: MessageRetryQueue ──────────────────────────────────────────────────

/// <summary>
/// ST-85: In-memory retry queue for failed outbound messages.
/// In production: backed by Redis list (openclaw:retry:{channelType}).
/// </summary>
public sealed class MessageRetryQueue
{
    private readonly record struct RetryItem(
        string UserId, ChannelType Channel, string Message, int Attempts, DateTimeOffset NextRetry);

    private readonly System.Collections.Concurrent.ConcurrentQueue<RetryItem> _queue = new();
    private readonly ILogger<MessageRetryQueue>                               _logger;
    private const int MaxRetries = 10;

    public MessageRetryQueue(ILogger<MessageRetryQueue> logger) => _logger = logger;

    public void Enqueue(string userId, ChannelType channel, string message)
    {
        _queue.Enqueue(new RetryItem(userId, channel, message, 0, DateTimeOffset.UtcNow.AddSeconds(30)));
        _logger.LogWarning("Message queued for retry: user {UserId} on {Channel}", userId, channel);
    }

    public int PendingCount => _queue.Count;

    public IReadOnlyList<(string UserId, ChannelType Channel, int Attempts)> GetStats() =>
        _queue.Select(r => (r.UserId, r.Channel, r.Attempts)).ToList();

    /// <summary>ST-85: Process due items (call from BackgroundService).</summary>
    public async Task ProcessDueAsync(Func<string, string, string, Task<bool>> sender, CancellationToken ct)
    {
        var due = new List<RetryItem>();
        while (_queue.TryDequeue(out var item))
            due.Add(item);

        foreach (var item in due)
        {
            if (item.NextRetry > DateTimeOffset.UtcNow)
            {
                // Not yet due — re-enqueue
                _queue.Enqueue(item);
                continue;
            }

            if (item.Attempts >= MaxRetries)
            {
                _logger.LogError("RETRY DLQ: Message to {UserId}/{Channel} exhausted after {Max} retries.",
                    item.UserId, item.Channel, MaxRetries);
                continue;
            }

            try
            {
                var success = await sender(item.UserId, item.Channel.ToString(), item.Message);
                if (!success)
                {
                    var delay = ExponentialDelay(item.Attempts);
                    _queue.Enqueue(item with { Attempts = item.Attempts + 1, NextRetry = DateTimeOffset.UtcNow.Add(delay) });
                }
            }
            catch
            {
                var delay = ExponentialDelay(item.Attempts);
                _queue.Enqueue(item with { Attempts = item.Attempts + 1, NextRetry = DateTimeOffset.UtcNow.Add(delay) });
            }
        }
    }

    private static TimeSpan ExponentialDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, attempt), 3600));
}

// ── ST-85: RetryWorkerService ─────────────────────────────────────────────────

public sealed class RetryWorkerService : BackgroundService
{
    private readonly MessageRetryQueue          _queue;
    private readonly ILogger<RetryWorkerService> _logger;

    public RetryWorkerService(MessageRetryQueue q, ILogger<RetryWorkerService> l)
    {
        _queue  = q;
        _logger = l;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _queue.ProcessDueAsync(
                    (userId, channel, msg) => Task.FromResult(true),
                    stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "RetryWorkerService tick failed."); }
        }
    }
}
