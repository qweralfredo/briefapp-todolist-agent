namespace BriefappTodoList.Api.Services.Sandbox;

using BriefappTodoList.Api.Domain.Sandbox;

/// <summary>
/// ST-03: Abstraction over Docker/Podman runtimes.
/// Enables mocking in unit tests and future runtime swaps.
/// </summary>
public interface IDockerRuntime
{
    /// <summary>Creates a container with the given config and returns the containerId.</summary>
    Task<string> CreateContainerAsync(SandboxConfig config, string containerName, CancellationToken ct = default);

    /// <summary>Starts a stopped/created container.</summary>
    Task StartContainerAsync(string containerId, CancellationToken ct = default);

    /// <summary>Gracefully stops a running container.</summary>
    Task StopContainerAsync(string containerId, CancellationToken ct = default);

    /// <summary>Force-removes a container (running or stopped).</summary>
    Task RemoveContainerAsync(string containerId, CancellationToken ct = default);

    /// <summary>Returns live resource stats for a running container.</summary>
    Task<ContainerStats> GetContainerStatsAsync(string containerId, CancellationToken ct = default);

    /// <summary>Executes a shell command inside a running container and captures output.</summary>
    Task<ExecResult> ExecInContainerAsync(
        string containerId,
        string command,
        string? workDir = null,
        int timeoutSeconds = 60,
        CancellationToken ct = default);

    /// <summary>Lists all container IDs with the given label (e.g. "briefapp-sandbox=true").</summary>
    Task<IReadOnlyList<string>> ListContainersByLabelAsync(string label, CancellationToken ct = default);
}
