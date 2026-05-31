namespace BriefappTodoList.Api.Domain.Queue;

/// <summary>
/// ST-23: Distributed task lock — ensures a single worker processes each task.
/// Unique constraint on (TaskId) WHERE Status = Active.
/// </summary>
public class TaskLockEntity
{
    public Guid   Id          { get; set; } = Guid.NewGuid();

    /// <summary>ID of the task being locked (string form of TaskMessageEntity.Id).</summary>
    public string TaskId      { get; set; } = string.Empty;

    /// <summary>Agent/worker identifier that holds this lock.</summary>
    public string WorkerId    { get; set; } = string.Empty;

    public LockStatus Status  { get; set; } = LockStatus.Active;

    public DateTimeOffset LockedAt    { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt   { get; set; }
    public DateTimeOffset HeartbeatAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Original TTL in minutes — used for heartbeat renewal.</summary>
    public int TimeoutMinutes { get; set; } = 30;
}
