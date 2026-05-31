namespace BriefappTodoList.Api.Domain.Queue;

/// <summary>
/// ST-29: Payload model for agent ACK/NACK responses.
/// Published by the agent after completing or failing a task.
/// </summary>
public record AckPayload(
    string TaskId,
    string WorkerId,

    /// <summary>"ack" = success, "nack" = failure.</summary>
    string Status,

    AckResult?  Result  = null,
    NackError?  Error   = null,
    AckMetrics? Metrics = null
);

public record AckResult(
    string? CommitHash    = null,
    int?    FilesChanged  = null,
    bool?   TestsPassed   = null,
    string? Summary       = null
);

public record NackError(
    NackCategory Category    = NackCategory.Unknown,
    string?      Message     = null,
    string?      StackTrace  = null,
    string?      RetryHint   = null
);

public record AckMetrics(
    int?    TokensUsed   = null,
    long?   DurationMs   = null,
    string? ModelUsed    = null,
    string? Provider     = null
);

/// <summary>ST-29: Result of processing an ACK/NACK request.</summary>
public record AckProcessResult(
    bool   Success,
    string Action,    // "completed" | "retrying" | "moved_to_dlq"
    int    RetryCount,
    string? Message = null
);
