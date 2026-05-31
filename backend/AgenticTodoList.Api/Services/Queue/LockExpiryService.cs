using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

/// <summary>
/// ST-27: Background service that auto-expires stale locks.
/// Runs every 30 seconds. When a lock expires (ExpiresAt &lt; Now),
/// it marks it Expired and requeues the task so another worker can pick it up.
/// </summary>
public sealed class LockExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ILogger<LockExpiryService>     _logger;
    private readonly TimeSpan                        _interval = TimeSpan.FromSeconds(30);

    public LockExpiryService(IServiceScopeFactory scopeFactory, ILogger<LockExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LockExpiryService starting.");
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ExpireLocksAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "LockExpiryService tick failed."); }
        }
    }

    private async Task ExpireLocksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher   = scope.ServiceProvider.GetRequiredService<TansuPublisherService>();

        var now = DateTimeOffset.UtcNow;
        var expired = await db.TaskLocks
            .Where(l => l.Status == LockStatus.Active && l.ExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count > 0)
            _logger.LogWarning("LockExpiry: {Count} expired lock(s) found.", expired.Count);

        foreach (var lockEntry in expired)
        {
            lockEntry.Status = LockStatus.Expired;

            // Resolve task message by TaskId and requeue if still processing
            if (Guid.TryParse(lockEntry.TaskId, out var taskGuid))
            {
                var task = await db.TaskMessages.FindAsync([taskGuid], ct);
                if (task is { Status: TaskMessageStatus.Processing or TaskMessageStatus.Pending })
                {
                    try { await publisher.RequeueAsync(task, ct: ct); }
                    catch (Exception ex)
                    { _logger.LogWarning(ex, "Failed to requeue task {Id} after lock expiry.", taskGuid); }
                }
            }
        }

        if (expired.Count > 0) await db.SaveChangesAsync(ct);
    }
}
