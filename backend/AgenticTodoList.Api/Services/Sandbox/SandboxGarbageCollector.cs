using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Sandbox;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.Sandbox;

/// <summary>
/// ST-11 + ST-12: Background service that:
///   1. Destroys sandboxes whose TTL has expired (every GC interval).
///   2. Detects and removes orphaned Docker containers (no DB entry).
/// GC interval is configurable via GarbageCollector:IntervalMinutes (default 5).
/// </summary>
public sealed class SandboxGarbageCollector : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SandboxGarbageCollector> _logger;
    private readonly TimeSpan             _interval;

    public SandboxGarbageCollector(
        IServiceScopeFactory scopeFactory,
        ILogger<SandboxGarbageCollector> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        var minutes = config.GetValue<int>("GarbageCollector:IntervalMinutes", 5);
        _interval   = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SandboxGarbageCollector starting. Interval: {Interval}",
            _interval);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunGcAsync(stoppingToken);
                await RemoveOrphansAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "GarbageCollector tick failed.");
            }
        }
    }

    // ── ST-11: TTL expiry ─────────────────────────────────────────────────────

    private async Task RunGcAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lifecycle   = scope.ServiceProvider.GetRequiredService<SandboxLifecycleService>();

        var now     = DateTimeOffset.UtcNow;
        var expired = await db.Sandboxes
            .Where(s => s.Status == SandboxStatus.Running && s.TimeoutAt != null && s.TimeoutAt < now)
            .ToListAsync(ct);

        if (expired.Count > 0)
            _logger.LogInformation("GC: {Count} expired sandbox(es) found.", expired.Count);

        foreach (var sandbox in expired)
        {
            try
            {
                await lifecycle.DestroySandboxAsync(sandbox.Id, ct);
                _logger.LogInformation("GC: Destroyed expired sandbox {Id}.", sandbox.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GC: Failed to destroy sandbox {Id}.", sandbox.Id);
            }
        }
    }

    // ── ST-12: Orphan detection ───────────────────────────────────────────────

    private async Task RemoveOrphansAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var docker = scope.ServiceProvider.GetRequiredService<IDockerRuntime>();

        // List all containers marked with briefapp-sandbox=true
        IReadOnlyList<string> dockerIds;
        try
        {
            dockerIds = await docker.ListContainersByLabelAsync("briefapp-sandbox=true", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GC: Could not list Docker containers for orphan check.");
            return;
        }

        if (dockerIds.Count == 0) return;

        // Find container IDs that have NO matching SandboxEntity
        var knownIds = await db.Sandboxes
            .Where(s => dockerIds.Contains(s.ContainerId))
            .Select(s => s.ContainerId)
            .ToListAsync(ct);

        var orphans = dockerIds.Except(knownIds).ToList();

        foreach (var orphanId in orphans)
        {
            try
            {
                await docker.RemoveContainerAsync(orphanId, ct);
                _logger.LogWarning("GC: Removed orphan container {ContainerId}.", orphanId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GC: Failed to remove orphan {ContainerId}.", orphanId);
            }
        }
    }
}
