namespace BriefappTodoList.Api.Domain.Sandbox;

/// <summary>
/// Stats snapshot from a running container.
/// </summary>
public record ContainerStats(
    double CpuPercent,
    double MemoryUsedMb,
    double MemoryLimitMb,
    long IoReadBytes,
    long IoWriteBytes
);

/// <summary>
/// Result of executing a command inside a container.
/// </summary>
public record ExecResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    long DurationMs
);
