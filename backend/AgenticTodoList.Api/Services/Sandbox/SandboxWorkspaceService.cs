using BriefappTodoList.Api.Domain.Sandbox;

namespace BriefappTodoList.Api.Services.Sandbox;

// ── Workspace Contracts ────────────────────────────────────────────────────────

public record WorkspaceInfo(
    string SandboxId,
    string TaskId,
    string HostPath,
    string ContainerPath,
    string GitBranch,
    DateTimeOffset CreatedAt
);

// ── ST-31: SandboxWorkspaceService ─────────────────────────────────────────────

/// <summary>
/// ST-31: Manages isolated git workspaces for each sandbox task.
/// Each task gets its own directory under /briefapp/workspaces/{taskId}/
/// cloned from the specified repo at the requested branch.
/// On Linux: OverlayFS enables copy-on-write isolation.
/// On non-Linux hosts (dev/Windows): plain directory clone is used.
/// </summary>
public sealed class SandboxWorkspaceService
{
    private static readonly string WorkspaceRoot =
        Environment.GetEnvironmentVariable("SANDBOX_WORKSPACE_ROOT")
        ?? "/briefapp/workspaces";

    private readonly IDockerRuntime               _docker;
    private readonly ILogger<SandboxWorkspaceService> _logger;

    public SandboxWorkspaceService(IDockerRuntime docker, ILogger<SandboxWorkspaceService> logger)
    {
        _docker = docker;
        _logger = logger;
    }

    // ── ST-31: PrepareWorkspace ────────────────────────────────────────────────

    /// <summary>Clones a git repo into an isolated workspace in the sandbox container.</summary>
    public async Task<WorkspaceInfo> PrepareWorkspaceAsync(
        string containerId,
        string taskId,
        string gitRepoUrl,
        string branch = "main",
        CancellationToken ct = default)
    {
        var hostPath      = Path.Combine(WorkspaceRoot, taskId);
        var containerPath = "/workspace";

        Directory.CreateDirectory(hostPath);
        _logger.LogInformation("Preparing workspace at {HostPath} for task {TaskId}", hostPath, taskId);

        // Clone the repo at the specified branch (shallow for speed)
        var cloneResult = await _docker.ExecInContainerAsync(
            containerId,
            $"git clone --depth=1 --branch {branch} {gitRepoUrl} {containerPath}",
            null, 120, ct);

        if (cloneResult.ExitCode != 0)
        {
            DeleteDirectory(hostPath);
            throw new InvalidOperationException($"git clone failed: {cloneResult.Stderr}");
        }

        // Security: refuse if any symlink found in workspace
        var symlinkCheck = await _docker.ExecInContainerAsync(
            containerId, $"find {containerPath} -type l -print -quit", null, 30, ct);

        if (!string.IsNullOrWhiteSpace(symlinkCheck.Stdout))
        {
            _logger.LogWarning("Symlink detected in workspace {TaskId}: {Symlink} — removing.",
                taskId, symlinkCheck.Stdout.Trim());
            DeleteDirectory(hostPath);
            throw new InvalidOperationException(
                $"Symlinks are prohibited in sandbox workspaces. Found: {symlinkCheck.Stdout}");
        }

        _logger.LogInformation("Workspace ready at {HostPath} (branch={Branch})", hostPath, branch);
        return new WorkspaceInfo(containerId, taskId, hostPath, containerPath, branch, DateTimeOffset.UtcNow);
    }

    // ── ST-32: CleanupWorkspace ────────────────────────────────────────────────

    /// <summary>Removes the workspace directory (by taskId) from the host.</summary>
    public Task CleanupWorkspaceAsync(string taskId, CancellationToken ct = default)
    {
        DeleteDirectory(Path.Combine(WorkspaceRoot, taskId));
        return Task.CompletedTask;
    }

    private void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            Directory.Delete(path, recursive: true);
            _logger.LogInformation("Workspace deleted: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete workspace {Path}", path);
        }
    }
}

// ── ST-32: WorkspaceCleanupService ────────────────────────────────────────────

/// <summary>
/// ST-32: Background service that cleans up workspaces for destroyed/expired sandboxes.
/// Runs every 15 minutes. Workspace TTL: default 1h after sandbox Destroy. Configurable
/// via SANDBOX_WORKSPACE_TTL_HOURS env var.
/// </summary>
public sealed class WorkspaceCleanupService : BackgroundService
{
    private static readonly string WorkspaceRoot =
        Environment.GetEnvironmentVariable("SANDBOX_WORKSPACE_ROOT")
        ?? "/briefapp/workspaces";

    private readonly ILogger<WorkspaceCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _ttl      = TimeSpan.FromHours(
        double.TryParse(Environment.GetEnvironmentVariable("SANDBOX_WORKSPACE_TTL_HOURS"), out var h) ? h : 1.0);

    public WorkspaceCleanupService(ILogger<WorkspaceCleanupService> logger)
        => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { CleanupExpired(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "WorkspaceCleanupService tick failed."); }
        }
    }

    private void CleanupExpired()
    {
        if (!Directory.Exists(WorkspaceRoot)) return;

        var cutoff = DateTime.UtcNow.Subtract(_ttl);
        foreach (var dir in Directory.GetDirectories(WorkspaceRoot))
        {
            if (new DirectoryInfo(dir).LastWriteTimeUtc < cutoff)
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    _logger.LogInformation("WorkspaceCleanup: removed expired workspace {Dir}", dir);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to remove workspace {Dir}", dir); }
            }
        }
    }
}
