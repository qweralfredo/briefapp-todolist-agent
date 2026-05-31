namespace BriefappTodoList.Api.Domain.Sandbox;

/// <summary>
/// ST-02: EF Core entity representing a sandbox container instance.
/// Indexed by (BoxId, Status) for efficient lifecycle queries.
/// </summary>
public class SandboxEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The Box (Project) that owns this sandbox.</summary>
    public Guid BoxId { get; set; }

    /// <summary>Optional: the work-item/task that triggered this sandbox.</summary>
    public string? TaskId { get; set; }

    public string ImageName { get; set; } = string.Empty;

    public SandboxStatus Status { get; set; } = SandboxStatus.Creating;

    /// <summary>Docker container ID returned by the runtime.</summary>
    public string ContainerId { get; set; } = string.Empty;

    public double CpuCores { get; set; } = 2.0;
    public int MemoryMb { get; set; } = 512;
    public SandboxNetworkMode NetworkMode { get; set; } = SandboxNetworkMode.Restricted;

    /// <summary>When the sandbox will be automatically destroyed (TTL).</summary>
    public DateTimeOffset? TimeoutAt { get; set; }

    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }

    /// <summary>ST-33: Network isolation policy applied to this container.</summary>
    public SandboxNetworkPolicy NetworkPolicy { get; set; } = SandboxNetworkPolicy.Restricted;

    /// <summary>Optional error message if Status == Error.</summary>
    public string? ErrorMessage { get; set; }
}
