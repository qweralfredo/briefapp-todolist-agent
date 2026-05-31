using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Sandbox;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.Sandbox;

/// <summary>
/// ST-04: Core sandbox service — Create, Get, and Exec operations.
/// Injected as Scoped in DI.
/// </summary>
public sealed class SandboxService
{
    private readonly AppDbContext _db;
    private readonly IDockerRuntime _docker;
    private readonly ILogger<SandboxService> _logger;

    public SandboxService(
        AppDbContext db,
        IDockerRuntime docker,
        ILogger<SandboxService> logger)
    {
        _db     = db;
        _docker = docker;
        _logger = logger;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and persists a new sandbox container.
    /// Validates config → resolves image → creates Docker container → saves entity.
    /// </summary>
    public async Task<SandboxEntity> CreateSandboxAsync(
        Guid boxId,
        SandboxConfig config,
        string? taskId = null,
        CancellationToken ct = default)
    {
        // Validate config
        if (!config.IsValid(out var errors))
            throw new ArgumentException($"Invalid sandbox config: {string.Join("; ", errors)}");

        // Resolve image alias
        var resolvedImage = SandboxImageRegistry.Resolve(config.ImageName);
        config.ImageName = resolvedImage;

        // Persist entity first (status = Creating)
        var entity = new SandboxEntity
        {
            BoxId       = boxId,
            TaskId      = taskId,
            ImageName   = resolvedImage,
            Status      = SandboxStatus.Creating,
            CpuCores    = config.CpuCores,
            MemoryMb    = config.MemoryMb,
            NetworkMode = config.NetworkMode,
        };
        _db.Sandboxes.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Create container
        try
        {
            var containerName = $"briefapp-sb-{entity.Id:N}";
            var containerId   = await _docker.CreateContainerAsync(config, containerName, ct);
            entity.ContainerId = containerId;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Sandbox {Id} created for Box {BoxId} with image {Image}",
                entity.Id, boxId, resolvedImage);

            return entity;
        }
        catch (Exception ex)
        {
            entity.Status       = SandboxStatus.Error;
            entity.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    public async Task<SandboxEntity?> GetSandboxAsync(Guid id, CancellationToken ct = default)
        => await _db.Sandboxes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SandboxEntity>> GetSandboxesByBoxAsync(
        Guid boxId, CancellationToken ct = default)
        => await _db.Sandboxes
            .AsNoTracking()
            .Where(s => s.BoxId == boxId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    // ── Stats ─────────────────────────────────────────────────────────────────

    public async Task<object> GetStatsAsync(CancellationToken ct = default)
    {
        var all = await _db.Sandboxes.AsNoTracking().ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var avgLifetime = all
            .Where(s => s.StoppedAt.HasValue)
            .Select(s => (s.StoppedAt!.Value - s.CreatedAt).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        return new
        {
            TotalActive    = all.Count(s => s.Status == SandboxStatus.Running),
            TotalCreated   = all.Count,
            TotalDestroyed = all.Count(s => s.Status == SandboxStatus.Destroyed),
            AvgLifetimeMinutes = Math.Round(avgLifetime, 1),
            ByImage  = all.GroupBy(s => s.ImageName)
                          .ToDictionary(g => g.Key, g => g.Count()),
            ByStatus = all.GroupBy(s => s.Status.ToString())
                          .ToDictionary(g => g.Key, g => g.Count()),
        };
    }
}
