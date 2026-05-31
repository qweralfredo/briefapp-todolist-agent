namespace BriefappTodoList.Api.Domain.Queue;

/// <summary>ST-36: Status of a Dead Letter Queue entry.</summary>
public enum DlqStatus
{
    /// <summary>Waiting for manual retry or drain.</summary>
    Pending     = 0,

    /// <summary>A retry has been submitted; awaiting ACK/NACK.</summary>
    Retrying    = 1,

    /// <summary>Successfully retried and acknowledged.</summary>
    Resolved    = 2,

    /// <summary>Poison message — too many retries, quarantined for manual inspection.</summary>
    Quarantined = 3,
}

/// <summary>
/// ST-36: EF Core entity representing a failed task in the Dead Letter Queue.
/// Auto-quarantined when RetryCount >= 5.
/// </summary>
public class DlqEntryEntity
{
    public Guid   Id            { get; set; } = Guid.NewGuid();

    /// <summary>The Box that owns the failed task.</summary>
    public Guid   BoxId         { get; set; }

    /// <summary>Tansu topic the task was originally published to.</summary>
    public string OriginalTopic { get; set; } = string.Empty;

    /// <summary>JSON-serialized task payload for resubmission.</summary>
    public string TaskPayload   { get; set; } = "{}";

    /// <summary>Original TaskMessageEntity.Id (string form).</summary>
    public string OriginalTaskId { get; set; } = string.Empty;

    /// <summary>Human-readable failure description (error category + message).</summary>
    public string FailureReason  { get; set; } = string.Empty;

    /// <summary>Number of times this task has been retried from the DLQ.</summary>
    public int    RetryCount     { get; set; }

    public DateTimeOffset FirstFailedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastFailedAt  { get; set; } = DateTimeOffset.UtcNow;

    public DlqStatus Status { get; set; } = DlqStatus.Pending;
}
