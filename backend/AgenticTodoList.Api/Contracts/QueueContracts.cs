namespace BriefappTodoList.Api.Contracts;

// ── BOX2: Transactional Queue Contracts (ST-17 / ST-24 / ST-25) ──────────────

/// <summary>ST-17: Request to publish a new task to the queue.</summary>
public record PublishTaskRequest(
    Guid     BoxId,
    object   Payload,
    string?  Source     = "api",
    string?  WorkItemId = null,
    int?     MaxRetries = 3
);

/// <summary>ST-24: Request to acquire a distributed lock on a task.</summary>
public record AcquireLockRequest(
    string TaskId,
    string WorkerId,
    int?   TimeoutMinutes = 30
);

/// <summary>ST-25: Request to renew a lock heartbeat.</summary>
public record HeartbeatLockRequest(string WorkerId);

/// <summary>ST-21: Queue stats response (mirrors TansuTopicStats).</summary>
public record QueueStatusResponse(
    string Topic,
    long   Pending,
    long   Processing,
    long   Completed
);

// ── BOX1-02: Workspace Contracts (ST-31) ─────────────────────────────────────

/// <summary>ST-31: Request to prepare a git workspace in a sandbox.</summary>
public record PrepareWorkspaceRequest(
    string  GitRepoUrl,
    string? Branch = "main"
);
