using Microsoft.Extensions.Configuration;

namespace BriefappTodoList.Api.Services;

/// <summary>
/// Validates X-Briefapp-Api-Key headers against configured keys (BL-14 SP-11).
/// Keys are stored in configuration as Auth:ApiKeys:0, Auth:ApiKeys:1, etc.
/// </summary>
public sealed class ApiKeyService
{
    private readonly HashSet<string> _validKeys;
    private readonly bool _isDevMode;

    public ApiKeyService(IConfiguration configuration)
    {
        _validKeys = configuration
            .GetSection("Auth:ApiKeys")
            .GetChildren()
            .Select(c => c.Value ?? string.Empty)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToHashSet(StringComparer.Ordinal);

        var mode = configuration["MODE"] ?? Environment.GetEnvironmentVariable("MODE");
        _isDevMode = mode == "dev";
    }

    public bool IsValid(string? key) =>
        _isDevMode || (!string.IsNullOrEmpty(key) && _validKeys.Contains(key));
}
