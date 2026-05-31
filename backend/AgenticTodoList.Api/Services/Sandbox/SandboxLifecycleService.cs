using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Sandbox;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.Sandbox;

/// <summary>
/// ST-09 + ST-10 + ST-06: Sandbox lifecycle management.
/// Handles Start, Stop, Destroy, and ExecInSandbox operations.
/// </summary>
public sealed class SandboxLifecycleService
{
    private readonly AppDbContext _db;
    private readonly IDockerRuntime _docker;
    private readonly ILogger<SandboxLifecycleService> _logger;

    public SandboxLifecycleService(
        AppDbContext db,
        IDockerRuntime docker,
        ILogger<SandboxLifecycleService> logger)
    {
        _db     = db;
        _docker = docker;
        _logger = logger;
    }

    // ── ST-09: Start ──────────────────────────────────────────────────────────

    /// <summary>Starts a sandbox that is in Creating status.</summary>
    public async Task<SandboxEntity> StartSandboxAsync(
        Guid sandboxId,
        CancellationToken ct = default)
    {
        var entity = await FindOrThrowAsync(sandboxId, ct);

        if (entity.Status != SandboxStatus.Creating)
            throw new InvalidOperationException(
                $"Cannot start sandbox in status '{entity.Status}'. Expected 'Creating'.");

        await _docker.StartContainerAsync(entity.ContainerId, ct);

        entity.Status    = SandboxStatus.Running;
        entity.TimeoutAt = DateTimeOffset.UtcNow.AddMinutes(entity.MemoryMb <= 512 ? 30 : 60);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sandbox {Id} started. TimeoutAt {TimeoutAt}", sandboxId, entity.TimeoutAt);
        return entity;
    }

    // ── ST-10: Stop ───────────────────────────────────────────────────────────

    /// <summary>Gracefully stops a running sandbox.</summary>
    public async Task<SandboxEntity> StopSandboxAsync(
        Guid sandboxId,
        CancellationToken ct = default)
    {
        var entity = await FindOrThrowAsync(sandboxId, ct);

        if (entity.Status != SandboxStatus.Running)
            throw new InvalidOperationException(
                $"Cannot stop sandbox in status '{entity.Status}'. Expected 'Running'.");

        await _docker.StopContainerAsync(entity.ContainerId, ct);

        entity.Status    = SandboxStatus.Stopped;
        entity.StoppedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sandbox {Id} stopped.", sandboxId);
        return entity;
    }

    // ── ST-10: Destroy ────────────────────────────────────────────────────────

    /// <summary>Force-removes a sandbox container regardless of current status.</summary>
    public async Task DestroySandboxAsync(
        Guid sandboxId,
        CancellationToken ct = default)
    {
        var entity = await FindOrThrowAsync(sandboxId, ct);

        if (!string.IsNullOrEmpty(entity.ContainerId))
        {
            try
            {
                await _docker.RemoveContainerAsync(entity.ContainerId, ct);
            }
            catch (Exception ex)
            {
                // Log but don't abort — still mark as Destroyed in DB
                _logger.LogWarning(ex, "Container remove failed for sandbox {Id}", sandboxId);
            }
        }

        entity.Status    = SandboxStatus.Destroyed;
        entity.StoppedAt = entity.StoppedAt ?? DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sandbox {Id} destroyed.", sandboxId);
    }

    // ── ST-06: ExecInSandbox ──────────────────────────────────────────────────

    /// <summary>Executes a shell command inside a running sandbox.</summary>
    public async Task<ExecResult> ExecInSandboxAsync(
        Guid sandboxId,
        string command,
        string? workDir    = null,
        int timeoutSeconds = 60,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be empty.", nameof(command));

        var entity = await FindOrThrowAsync(sandboxId, ct);

        if (entity.Status != SandboxStatus.Running)
            throw new InvalidOperationException(
                $"Sandbox {sandboxId} is not Running (current: {entity.Status}).");

        var result = await _docker.ExecInContainerAsync(
            entity.ContainerId,
            command,
            workDir,
            timeoutSeconds,
            ct);

        _logger.LogInformation(
            "Exec in sandbox {Id}: exit={Code} duration={Ms}ms",
            sandboxId, result.ExitCode, result.DurationMs);

        return result;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<SandboxEntity> FindOrThrowAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Sandboxes.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null)
            throw new KeyNotFoundException($"Sandbox {id} not found.");
        return entity;
    }
}
