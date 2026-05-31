namespace BriefappTodoList.Api.Domain.Sandbox;

/// <summary>ST-33: Network isolation policy for sandbox containers.</summary>
public enum SandboxNetworkPolicy
{
    /// <summary>No external access — --network=none.</summary>
    Offline    = 0,

    /// <summary>Whitelisted package registries only (npm, pypi, nuget, github).</summary>
    Restricted = 1,

    /// <summary>Full internet access — use only for trusted trusted workloads.</summary>
    Full       = 2,
}
