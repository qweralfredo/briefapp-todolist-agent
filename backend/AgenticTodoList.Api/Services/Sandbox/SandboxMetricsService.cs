using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Sandbox;

namespace BriefappTodoList.Api.Services.Sandbox;

// ── DockerStatsResult ──────────────────────────────────────────────────────────

internal sealed record DockerStatsResult(
    double CpuPercent,
    double MemoryUsageMb,
    double MemoryPercent,
    long   NetworkRxBytes,
    long   NetworkTxBytes,
    long   DiskReadBytes,
    long   DiskWriteBytes
);

// ── ST-34: SandboxMetricsService ──────────────────────────────────────────────

/// <summary>
/// ST-34: Collects docker stats for running sandboxes and persists snapshots.
/// Uses `docker stats --no-stream --format json` to capture a single reading.
/// </summary>
public sealed class SandboxMetricsService
{
    private readonly AppDbContext                    _db;
    private readonly ILogger<SandboxMetricsService> _logger;

    public SandboxMetricsService(AppDbContext db, ILogger<SandboxMetricsService> logger)
    {
        _db    = db;
        _logger = logger;
    }

    /// <summary>Collect a single metrics snapshot for a sandbox and persist it.</summary>
    public async Task<SandboxMetricsDto?> CollectAndPersistAsync(
        Guid   sandboxId,
        string containerId,
        CancellationToken ct = default)
    {
        var raw = await RunDockerStatsAsync(containerId, ct);
        if (raw is null) return null;

        var sandbox = await _db.Sandboxes.FindAsync([sandboxId], ct);
        var uptime  = sandbox?.StartedAt.HasValue == true
            ? (long)(DateTimeOffset.UtcNow - sandbox.StartedAt!.Value).TotalSeconds
            : 0L;

        var entity = new SandboxMetricSnapshotEntity
        {
            SandboxId      = sandboxId,
            CpuPercent     = raw.CpuPercent,
            MemoryMb       = raw.MemoryUsageMb,
            MemoryPercent  = raw.MemoryPercent,
            NetworkRxBytes = raw.NetworkRxBytes,
            NetworkTxBytes = raw.NetworkTxBytes,
            DiskReadBytes  = raw.DiskReadBytes,
            DiskWriteBytes = raw.DiskWriteBytes,
            UptimeSeconds  = uptime,
        };

        _db.SandboxMetricSnapshots.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    /// <summary>ST-34: Returns the last 100 snapshots for a sandbox.</summary>
    public async Task<IReadOnlyList<SandboxMetricsDto>> GetHistoryAsync(
        Guid sandboxId,
        int  limit = 100,
        CancellationToken ct = default)
    {
        var rows = await _db.SandboxMetricSnapshots
            .AsNoTracking()
            .Where(s => s.SandboxId == sandboxId)
            .OrderByDescending(s => s.CapturedAt)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    private static SandboxMetricsDto ToDto(SandboxMetricSnapshotEntity e) => new(
        e.CpuPercent, e.MemoryMb, e.MemoryPercent,
        e.NetworkRxBytes, e.NetworkTxBytes,
        e.DiskReadBytes, e.DiskWriteBytes,
        e.UptimeSeconds, e.CapturedAt);

    private async Task<DockerStatsResult?> RunDockerStatsAsync(string containerId, CancellationToken ct)
    {
        try
        {
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = "docker",
                    Arguments = $"stats --no-stream --format \"{{{{json .}}}}\" {containerId}",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                }
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return null;

            var json = JsonNode.Parse(output.Trim());
            if (json is null) return null;

            // docker stats JSON: CPUPerc, MemUsage, MemPerc, NetIO, BlockIO
            var cpuStr  = json["CPUPerc"]?.GetValue<string>()?.TrimEnd('%') ?? "0";
            var memPerc = json["MemPerc"]?.GetValue<string>()?.TrimEnd('%') ?? "0";
            var memUsage = ParseMemory(json["MemUsage"]?.GetValue<string>() ?? "0MiB / 0MiB");
            var (netRx, netTx)     = ParseIOPair(json["NetIO"]?.GetValue<string>() ?? "0B / 0B");
            var (diskR, diskW)     = ParseIOPair(json["BlockIO"]?.GetValue<string>() ?? "0B / 0B");

            return new DockerStatsResult(
                double.TryParse(cpuStr,  out var cpu)  ? cpu  : 0,
                memUsage,
                double.TryParse(memPerc, out var mPct) ? mPct : 0,
                netRx, netTx, diskR, diskW);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "docker stats failed for container {Container}", containerId);
            return null;
        }
    }

    private static double ParseMemory(string raw)
    {
        // "256MiB / 512MiB" → 256.0 MB
        var part = raw.Split('/')[0].Trim();
        return ParseBytes(part) / 1_048_576.0;
    }

    private static (long, long) ParseIOPair(string raw)
    {
        var parts = raw.Split('/');
        return (ParseBytes(parts[0].Trim()), parts.Length > 1 ? ParseBytes(parts[1].Trim()) : 0L);
    }

    private static long ParseBytes(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim();
        if (s.EndsWith("GiB", StringComparison.OrdinalIgnoreCase)) return (long)(double.Parse(s[..^3]) * 1_073_741_824);
        if (s.EndsWith("MiB", StringComparison.OrdinalIgnoreCase)) return (long)(double.Parse(s[..^3]) * 1_048_576);
        if (s.EndsWith("kB",  StringComparison.OrdinalIgnoreCase)) return (long)(double.Parse(s[..^2]) * 1000);
        if (s.EndsWith("MB",  StringComparison.OrdinalIgnoreCase)) return (long)(double.Parse(s[..^2]) * 1_000_000);
        if (s.EndsWith("GB",  StringComparison.OrdinalIgnoreCase)) return (long)(double.Parse(s[..^2]) * 1_000_000_000);
        if (s.EndsWith("B",   StringComparison.OrdinalIgnoreCase)) return (long)double.Parse(s[..^1]);
        return 0L;
    }
}

// ── ST-34: SandboxMetricsCollectorService ─────────────────────────────────────

/// <summary>
/// ST-34: BackgroundService that collects metrics for all Running sandboxes every 30s.
/// </summary>
public sealed class SandboxMetricsCollectorService : BackgroundService
{
    private readonly IServiceScopeFactory                    _scopeFactory;
    private readonly ILogger<SandboxMetricsCollectorService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public SandboxMetricsCollectorService(IServiceScopeFactory s, ILogger<SandboxMetricsCollectorService> l)
    {
        _scopeFactory = s;
        _logger       = l;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await CollectAllAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "MetricsCollector tick failed."); }
        }
    }

    private async Task CollectAllAsync(CancellationToken ct)
    {
        using var scope   = _scopeFactory.CreateScope();
        var db            = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var metricsSvc    = scope.ServiceProvider.GetRequiredService<SandboxMetricsService>();

        var running = await db.Sandboxes
            .AsNoTracking()
            .Where(s => s.Status == SandboxStatus.Running && s.ContainerId != null)
            .Select(s => new { s.Id, s.ContainerId })
            .ToListAsync(ct);

        foreach (var s in running)
        {
            try { await metricsSvc.CollectAndPersistAsync(s.Id, s.ContainerId!, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect metrics for sandbox {Id}.", s.Id); }
        }
    }
}
