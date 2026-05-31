using BriefappTodoList.Api.Domain.Sandbox;
using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;

namespace BriefappTodoList.Api.Services.Sandbox;

// ── Docker network names ───────────────────────────────────────────────────────

public static class SandboxNetworks
{
    public const string Restricted = "briefapp-restricted";
    public const string Full       = "bridge";
    public const string None       = "none";

    public static readonly string[] AllowedDomains =
    [
        "registry.npmjs.org",
        "pypi.org", "files.pythonhosted.org",
        "api.nuget.org", "www.nuget.org",
        "github.com", "raw.githubusercontent.com",
        "ghcr.io", "registry.hub.docker.com",
    ];
}

// ── NetworkPolicyDto ───────────────────────────────────────────────────────────

public record NetworkPolicyDto(
    Guid     SandboxId,
    bool     AllowInternet,
    string[] AllowedHosts,
    string   DnsProvider,
    int      BandwidthLimitKbps
);

// ── ST-33: SandboxNetworkPolicyService ────────────────────────────────────────

/// <summary>
/// ST-33: Resolves Docker network policy for sandboxes.
/// Also exposes GetPolicyAsync for the Sandbox Monitor dashboard (ST-35).
/// </summary>
public sealed class SandboxNetworkPolicyService
{
    private readonly AppDbContext                          _db;
    private readonly ILogger<SandboxNetworkPolicyService> _logger;

    public SandboxNetworkPolicyService(AppDbContext db, ILogger<SandboxNetworkPolicyService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>Returns the Docker --network flag value for a given policy.</summary>
    public string GetDockerNetworkName(SandboxNetworkPolicy policy) => policy switch
    {
        SandboxNetworkPolicy.Offline    => SandboxNetworks.None,
        SandboxNetworkPolicy.Restricted => SandboxNetworks.Restricted,
        SandboxNetworkPolicy.Full       => SandboxNetworks.Full,
        _                               => SandboxNetworks.None,
    };

    // ── ST-35: GetPolicyAsync ─────────────────────────────────────────────────

    /// <summary>ST-35: Returns the effective network policy DTO for the Sandbox Monitor.</summary>
    public async Task<NetworkPolicyDto?> GetPolicyAsync(Guid sandboxId, CancellationToken ct = default)
    {
        var sandbox = await _db.Sandboxes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sandboxId, ct);

        if (sandbox is null) return null;

        var allowInternet      = sandbox.NetworkPolicy == SandboxNetworkPolicy.Full;
        var allowedHosts       = sandbox.NetworkPolicy == SandboxNetworkPolicy.Restricted
            ? SandboxNetworks.AllowedDomains
            : allowInternet ? ["*"] : [];
        var dnsProvider        = sandbox.NetworkPolicy == SandboxNetworkPolicy.Offline ? "none" : "8.8.8.8";
        const int BandwidthKbps = 0;

        return new NetworkPolicyDto(sandboxId, allowInternet, allowedHosts, dnsProvider, BandwidthKbps);
    }

    // ── EnsureRestrictedNetwork ───────────────────────────────────────────────

    public async Task EnsureRestrictedNetworkAsync(CancellationToken ct = default)
    {
        try
        {
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = "docker",
                    Arguments              = $"network inspect {SandboxNetworks.Restricted}",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                var create = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = "docker",
                        Arguments       = $"network create --driver bridge --internal {SandboxNetworks.Restricted}",
                        UseShellExecute = false,
                        CreateNoWindow  = true,
                    }
                };
                create.Start();
                await create.WaitForExitAsync(ct);
                _logger.LogInformation("Created Docker network: {Network}", SandboxNetworks.Restricted);
            }
            else
            {
                _logger.LogDebug("Docker network {Network} already exists.", SandboxNetworks.Restricted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure Docker network {Network}.", SandboxNetworks.Restricted);
        }
    }
}
