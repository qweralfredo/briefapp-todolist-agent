namespace BriefappTodoList.Api.Domain.Queue;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>ST-15: Status of a task message in the queue.</summary>
public enum TaskMessageStatus
{
    Pending    = 0,
    Processing = 1,
    Completed  = 2,
    Failed     = 3,
    Requeued   = 4,
}

/// <summary>ST-29: Category of agent failure for NACK routing.</summary>
public enum NackCategory
{
    CompilationError    = 0,
    TestFailure         = 1,
    ApiTimeout          = 2,
    Hallucination       = 3,
    DependencyError     = 4,
    ResourceExhaustion  = 5,
    Unknown             = 99,
}

/// <summary>ST-23: Status of a distributed task lock.</summary>
public enum LockStatus
{
    Active   = 0,
    Expired  = 1,
    Released = 2,
}
