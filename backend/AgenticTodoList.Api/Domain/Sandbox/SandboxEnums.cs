namespace BriefappTodoList.Api.Domain.Sandbox;

/// <summary>Network mode for sandbox containers.</summary>
public enum SandboxNetworkMode
{
    /// <summary>Only whitelisted domains are accessible.</summary>
    Restricted = 0,

    /// <summary>No network access at all.</summary>
    Offline = 1,

    /// <summary>Full unrestricted network access (dev only).</summary>
    Full = 2
}

/// <summary>Lifecycle status of a sandbox container.</summary>
public enum SandboxStatus
{
    Creating      = 0,
    Running       = 1,
    Stopped       = 2,
    Destroyed     = 3,
    Error         = 4
}
