namespace BriefappTodoList.Api.Services.Sandbox;

/// <summary>
/// ST-07: Registry of supported base Docker images.
/// Maps human-friendly aliases to full image references.
/// Pull is triggered automatically on first use.
/// </summary>
public static class SandboxImageRegistry
{
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["node"]     = "node:20-slim",
        ["python"]   = "python:3.12-slim",
        ["dotnet"]   = "mcr.microsoft.com/dotnet/sdk:10.0",
        ["dotnet10"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        ["ubuntu"]   = "ubuntu:24.04",
        ["alpine"]   = "alpine:3.20",
    };

    /// <summary>All registered aliases (for documentation/validation).</summary>
    public static IReadOnlyDictionary<string, string> Aliases => _aliases;

    /// <summary>
    /// Resolves an alias or validates a full image name.
    /// Returns the full image reference, or throws if the name is not recognized.
    /// </summary>
    public static string Resolve(string imageOrAlias)
    {
        if (string.IsNullOrWhiteSpace(imageOrAlias))
            throw new ArgumentException("Image name cannot be empty.", nameof(imageOrAlias));

        // Alias lookup
        if (_aliases.TryGetValue(imageOrAlias, out var full))
            return full;

        // Accept full image names (must contain ':' or '/')
        if (imageOrAlias.Contains(':') || imageOrAlias.Contains('/'))
            return imageOrAlias;

        throw new InvalidOperationException(
            $"Image '{imageOrAlias}' is not in the registry. " +
            $"Valid aliases: {string.Join(", ", _aliases.Keys)}");
    }

    /// <summary>Checks if an alias or image name is registered/valid.</summary>
    public static bool IsValid(string imageOrAlias)
    {
        try
        {
            Resolve(imageOrAlias);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
