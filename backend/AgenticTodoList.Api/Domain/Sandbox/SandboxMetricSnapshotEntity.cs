namespace BriefappTodoList.Api.Domain.Sandbox;

/// <summary>
/// ST-34: Single metrics snapshot captured via `docker stats --no-stream`.
/// Persisted every 30s by SandboxMetricsCollectorService.
/// </summary>
public class SandboxMetricSnapshotEntity
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public Guid   SandboxId   { get; set; }

    public double CpuPercent     { get; set; }
    public double MemoryMb       { get; set; }
    public double MemoryPercent  { get; set; }
    public long   NetworkRxBytes { get; set; }
    public long   NetworkTxBytes { get; set; }
    public long   DiskReadBytes  { get; set; }
    public long   DiskWriteBytes { get; set; }
    public long   UptimeSeconds  { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>ST-34: DTO returned by GET /api/sandbox/{id}/metrics.</summary>
public record SandboxMetricsDto(
    double CpuPercent,
    double MemoryMb,
    double MemoryPercent,
    long   NetworkRxBytes,
    long   NetworkTxBytes,
    long   DiskReadBytes,
    long   DiskWriteBytes,
    long   UptimeSeconds,
    DateTimeOffset CapturedAt
);
