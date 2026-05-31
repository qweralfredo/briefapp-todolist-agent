using System.Diagnostics;
using System.Text.Json;
using BriefappTodoList.Api.Domain.Sandbox;

namespace BriefappTodoList.Api.Services.Sandbox;

/// <summary>
/// ST-03: Docker CLI runtime — wraps docker CLI via Process.Start.
/// Uses the "briefapp-sandbox=true" label on every container for orphan detection.
/// </summary>
public sealed class DockerCliRuntime : IDockerRuntime
{
    private readonly ILogger<DockerCliRuntime> _logger;

    public DockerCliRuntime(ILogger<DockerCliRuntime> logger)
    {
        _logger = logger;
    }

    // ── Create ────────────────────────────────────────────────────────────────
    public async Task<string> CreateContainerAsync(
        SandboxConfig config,
        string containerName,
        CancellationToken ct = default)
    {
        var networkArg = config.NetworkMode switch
        {
            SandboxNetworkMode.Offline     => "--network none",
            SandboxNetworkMode.Full        => "",
            _                               => "--network host", // Restricted: controlled via iptables
        };

        var args =
            $"create " +
            $"--name {containerName} " +
            $"--cpus={config.CpuCores} " +
            $"--memory={config.MemoryMb}m " +
            $"--memory-swap={config.MemoryMb}m " +
            $"--label briefapp-sandbox=true " +
            $"--workdir={config.WorkDir} " +
            $"{networkArg} " +
            $"{config.ImageName} " +
            $"sleep infinity"; // Keeps container alive for exec calls

        var output = await RunDockerAsync(args, ct);
        var containerId = output.Trim();

        if (string.IsNullOrEmpty(containerId))
            throw new InvalidOperationException("Docker create returned empty container ID.");

        _logger.LogInformation("Created container {Name} → {Id}", containerName, containerId[..12]);
        return containerId;
    }

    // ── Start ─────────────────────────────────────────────────────────────────
    public async Task StartContainerAsync(string containerId, CancellationToken ct = default)
    {
        await RunDockerAsync($"start {containerId}", ct);
        _logger.LogInformation("Started container {Id}", containerId[..12]);
    }

    // ── Stop ──────────────────────────────────────────────────────────────────
    public async Task StopContainerAsync(string containerId, CancellationToken ct = default)
    {
        await RunDockerAsync($"stop --time 10 {containerId}", ct);
        _logger.LogInformation("Stopped container {Id}", containerId[..12]);
    }

    // ── Remove ────────────────────────────────────────────────────────────────
    public async Task RemoveContainerAsync(string containerId, CancellationToken ct = default)
    {
        await RunDockerAsync($"rm -f {containerId}", ct);
        _logger.LogInformation("Removed container {Id}", containerId[..12]);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────
    public async Task<ContainerStats> GetContainerStatsAsync(string containerId, CancellationToken ct = default)
    {
        // --no-stream returns a single snapshot instead of a live stream
        var raw = await RunDockerAsync(
            $"stats --no-stream --format \"{{{{json .}}}}\" {containerId}", ct);

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            double ParseCpu(string s)
            {
                s = s.TrimEnd('%');
                return double.TryParse(s, out var v) ? v : 0;
            }

            double ParseMem(string s)
            {
                // "512MiB" → 512
                var num = string.Concat(s.TakeWhile(c => char.IsDigit(c) || c == '.'));
                return double.TryParse(num, out var v) ? v : 0;
            }

            var cpuStr  = root.TryGetProperty("CPUPerc",  out var c) ? c.GetString() ?? "0%" : "0%";
            var memStr  = root.TryGetProperty("MemUsage", out var m) ? m.GetString() ?? "0MiB / 0MiB" : "0MiB / 0MiB";
            var memParts = memStr.Split('/');

            return new ContainerStats(
                CpuPercent:    ParseCpu(cpuStr),
                MemoryUsedMb:  ParseMem(memParts[0].Trim()),
                MemoryLimitMb: memParts.Length > 1 ? ParseMem(memParts[1].Trim()) : 0,
                IoReadBytes:   0,
                IoWriteBytes:  0
            );
        }
        catch
        {
            return new ContainerStats(0, 0, 0, 0, 0);
        }
    }

    // ── Exec ──────────────────────────────────────────────────────────────────
    public async Task<ExecResult> ExecInContainerAsync(
        string containerId,
        string command,
        string? workDir = null,
        int timeoutSeconds = 60,
        CancellationToken ct = default)
    {
        var workDirArg = workDir is not null ? $"--workdir {workDir}" : string.Empty;
        var args = $"exec {workDirArg} {containerId} sh -c \"{command.Replace("\"", "\\\"")}\"";

        var sw = Stopwatch.StartNew();
        var (stdout, stderr, exitCode) = await RunDockerWithOutputAsync(args, timeoutSeconds, ct);
        sw.Stop();

        return new ExecResult(exitCode, stdout, stderr, sw.ElapsedMilliseconds);
    }

    // ── List by label ─────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<string>> ListContainersByLabelAsync(string label, CancellationToken ct = default)
    {
        var raw = await RunDockerAsync($"ps -aq --filter label={label}", ct);
        return raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Runs a docker command and returns stdout. Throws on non-zero exit.</summary>
    private async Task<string> RunDockerAsync(string args, CancellationToken ct)
    {
        var (stdout, stderr, exitCode) = await RunDockerWithOutputAsync(args, 60, ct);
        if (exitCode != 0)
            throw new InvalidOperationException($"docker {args[..Math.Min(40, args.Length)]} failed ({exitCode}): {stderr}");
        return stdout;
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> RunDockerWithOutputAsync(
        string args,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = "docker",
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };

        proc.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);

        await proc.WaitForExitAsync(cts.Token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (stdout.Trim(), stderr.Trim(), proc.ExitCode);
    }
}
