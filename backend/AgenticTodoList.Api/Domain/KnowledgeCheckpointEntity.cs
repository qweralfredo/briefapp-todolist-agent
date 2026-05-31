namespace BriefappTodoList.Api.Domain;

public class KnowledgeCheckpointEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string ContextSnapshot { get; set; } = string.Empty;
    public string Decisions { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public string NextActions { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

