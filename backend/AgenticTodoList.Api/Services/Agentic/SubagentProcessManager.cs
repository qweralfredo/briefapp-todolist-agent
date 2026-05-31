using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BriefappTodoList.Api.Services.Agentic;

public interface ISubagentProcessManager
{
    void TriggerSubagent(Guid taskId, string taskTitle, string taskDescription);
}

public class SubagentProcessManager : ISubagentProcessManager
{
    private readonly ILogger<SubagentProcessManager> _logger;
    private readonly string _scriptPath;

    public SubagentProcessManager(ILogger<SubagentProcessManager> logger)
    {
        _logger = logger;
        // Caminho relativo à execução do projeto (idealmente configurado)
        _scriptPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "ops", "scripts", "run-subagent-worktree.ps1"));
    }

    public void TriggerSubagent(Guid taskId, string taskTitle, string taskDescription)
    {
        var prompt = $"Title: {taskTitle}\nDescription: {taskDescription}";

        _logger.LogInformation("Triggering subagent for Task {TaskId} via script: {ScriptPath}", taskId, _scriptPath);

        Task.Run(() =>
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{_scriptPath}\" -TaskId \"{taskId}\" -TaskPrompt \"{prompt.Replace("\"", "\"\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start PowerShell process for Task {TaskId}", taskId);
                    return;
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Subagent script exited with code {ExitCode} for Task {TaskId}. Error: {Error}", process.ExitCode, taskId, error);
                }
                else
                {
                    _logger.LogInformation("Subagent script completed successfully for Task {TaskId}. Output: {Output}", taskId, output);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while triggering subagent for Task {TaskId}", taskId);
            }
        });
    }
}
