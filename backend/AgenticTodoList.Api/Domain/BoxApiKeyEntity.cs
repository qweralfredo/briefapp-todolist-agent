using System.Security.Cryptography;
using System.Text;

namespace BriefappTodoList.Api.Domain;

public class BoxApiKeyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty; // friendly label
    public string Prefix { get; set; } = string.Empty; // first 8 chars for display
    public string KeyHash { get; set; } = string.Empty; // SHA256 hash
    public string Scopes { get; set; } = string.Empty; // CSV: read,write,admin
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }

    public ProjectEntity? Project { get; set; }

    public static string GenerateKey() => $"pbx_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
    public static string HashKey(string rawKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
}
