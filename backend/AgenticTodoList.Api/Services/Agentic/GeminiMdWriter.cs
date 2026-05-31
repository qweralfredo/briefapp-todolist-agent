using System.Text;
using BriefappTodoList.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.Agentic;

public class GeminiMdWriter : IGeminiMdWriter
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiMdWriter> _logger;

    public GeminiMdWriter(AppDbContext db, IConfiguration configuration, ILogger<GeminiMdWriter> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task WriteSprintContextAsync(Guid projectId, string sprintName, string goal, IEnumerable<GeminiTaskDto> tasks, CancellationToken ct = default)
    {
        try
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
            if (project == null || string.IsNullOrWhiteSpace(project.LocalPath))
            {
                _logger.LogWarning("Project {ProjectId} not found or has no LocalPath configured.", projectId);
                return;
            }

            var geminiMdPath = Path.Combine(project.LocalPath, "GEMINI.md");
            
            var sb = new StringBuilder();
            sb.AppendLine("# Briefapp Todo List - Contexto da Sprint");
            sb.AppendLine();
            sb.AppendLine("> [!NOTE]");
            sb.AppendLine("> Este arquivo `GEMINI.md` \u00e9 atualizado dinamicamente pelo backend do Briefapp sempre que uma Sprint inicia ou tarefas mudam de status.");
            sb.AppendLine("> O Gemini CLI l\u00ea este arquivo para obter contexto do que precisa ser feito em cada projeto.");
            sb.AppendLine();
            sb.AppendLine("## Active Sprint");
            sb.AppendLine($"**Sprint:** {sprintName}");
            sb.AppendLine($"**Status:** In Progress");
            sb.AppendLine($"**Goal:** {goal}");
            sb.AppendLine();
            sb.AppendLine("## Tarefas Pendentes (Subagents Tasks)");
            
            foreach (var task in tasks)
            {
                var branchInfo = string.IsNullOrWhiteSpace(task.Branch) ? "" : $" (`{task.Branch}`)";
                var assigneeInfo = string.IsNullOrWhiteSpace(task.Assignee) ? "" : $" - Assignee: @{task.Assignee}";
                sb.AppendLine($"- **#{task.Id.ToString().Substring(0, 8)}**: `{task.Title}`{branchInfo}{assigneeInfo}");
                if (!string.IsNullOrWhiteSpace(task.Description))
                {
                    sb.AppendLine($"  > {task.Description}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Guidelines para o Gemini CLI");
            sb.AppendLine("- Use `gemini --prompt` para rodar tarefas em Headless mode no background.");
            sb.AppendLine("- Para editar UI, delegue para `@frontend` ou crie os artefatos.");
            sb.AppendLine("- N\u00e3o altere o banco de dados diretamente sem autoriza\u00e7\u00e3o.");
            sb.AppendLine();
            sb.AppendLine("## Conhecimento (Knowledge Base)");
            sb.AppendLine($"- Stack: {project.TechStack ?? "N\u00e3o definida"}");
            
            await File.WriteAllTextAsync(geminiMdPath, sb.ToString(), ct);
            _logger.LogInformation("Successfully wrote {FileName}", geminiMdPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write GEMINI.md for project {ProjectId}", projectId);
        }
    }
}
