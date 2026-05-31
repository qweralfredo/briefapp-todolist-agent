using System.Text.Json.Serialization;

namespace BriefappTodoList.Api.Domain.Queue;

/// <summary>
/// ST-15: Core task message published to Tansu topic briefapp.{boxId}.tasks.
/// Persisted in DB for audit, retry tracking, and DLQ support.
/// </summary>
public class TaskMessageEntity
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public Guid   BoxId       { get; set; }

    /// <summary>Tansu topic this message was published to.</summary>
    public string Topic       { get; set; } = string.Empty;

    /// <summary>Full JSON payload sent to the worker agent.</summary>
    public string Payload     { get; set; } = "{}";

    public TaskMessageStatus Status { get; set; } = TaskMessageStatus.Pending;

    /// <summary>Source channel: api | openclaw | scheduled | manual.</summary>
    public string Source      { get; set; } = "api";

    /// <summary>Optional backlog work item that generated this task.</summary>
    public string? WorkItemId { get; set; }

    public int    RetryCount  { get; set; } = 0;
    public int    MaxRetries  { get; set; } = 3;

    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Scheduled delivery time (for delayed retry).</summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>ST-47: Processing duration in milliseconds (set on ACK).</summary>
    public long? DurationMs { get; set; }
}
