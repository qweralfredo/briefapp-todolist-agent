namespace BriefappTodoList.Api.Services.Agentic;

public interface IGeminiMdWriter
{
    Task WriteSprintContextAsync(Guid projectId, string sprintName, string goal, IEnumerable<GeminiTaskDto> tasks, CancellationToken ct = default);
}

public class GeminiTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
}
