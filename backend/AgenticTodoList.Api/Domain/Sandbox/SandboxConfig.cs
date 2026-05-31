using System.ComponentModel.DataAnnotations;

namespace BriefappTodoList.Api.Domain.Sandbox;

/// <summary>
/// ST-01: Configuration model for creating a sandbox container.
/// Validates resource limits before container creation.
/// </summary>
public class SandboxConfig
{
    // ── Image ────────────────────────────────────────────────────────────────
    /// <summary>Docker image name or alias (e.g. "node", "python", "dotnet").</summary>
    [Required]
    public string ImageName { get; set; } = string.Empty;

    // ── Resource limits ──────────────────────────────────────────────────────
    /// <summary>CPU cores limit. Min 0.5, max 4.</summary>
    [Range(0.5, 4.0)]
    public double CpuCores { get; set; } = 2.0;

    /// <summary>RAM limit in megabytes. Min 128, max 2048.</summary>
    [Range(128, 2048)]
    public int MemoryMb { get; set; } = 512;

    /// <summary>Auto-destroy timeout in minutes. Min 1, max 120.</summary>
    [Range(1, 120)]
    public int TimeoutMinutes { get; set; } = 30;

    // ── Network ──────────────────────────────────────────────────────────────
    public SandboxNetworkMode NetworkMode { get; set; } = SandboxNetworkMode.Restricted;

    /// <summary>Working directory inside the container.</summary>
    public string WorkDir { get; set; } = "/app";

    // ── Validation helper ────────────────────────────────────────────────────
    /// <summary>Validates config and returns a list of validation errors.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ImageName))
            errors.Add("ImageName is required.");

        if (CpuCores is < 0.5 or > 4.0)
            errors.Add("CpuCores must be between 0.5 and 4.");

        if (MemoryMb is < 128 or > 2048)
            errors.Add("MemoryMb must be between 128 and 2048.");

        if (TimeoutMinutes is < 1 or > 120)
            errors.Add("TimeoutMinutes must be between 1 and 120.");

        return errors;
    }

    /// <summary>Returns true if config is valid.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        errors = Validate();
        return errors.Count == 0;
    }
}
