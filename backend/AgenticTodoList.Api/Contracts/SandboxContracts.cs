using BriefappTodoList.Api.Domain.Sandbox;

namespace BriefappTodoList.Api.Contracts;

// ── ST-05: Sandbox REST API request/response DTOs ────────────────────────────

/// <summary>Request body for POST /api/sandbox</summary>
public record CreateSandboxRequest(
    Guid   BoxId,
    string ImageName,
    double? CpuCores        = null,
    int?   MemoryMb         = null,
    int?   TimeoutMinutes   = null,
    SandboxNetworkMode? NetworkMode = null,
    string? TaskId          = null,
    string? WorkDir         = null
);

/// <summary>Request body for POST /api/sandbox/{id}/exec</summary>
public record SandboxExecRequest(
    string Command,
    string? WorkDir        = null,
    int    TimeoutSeconds  = 60
);
