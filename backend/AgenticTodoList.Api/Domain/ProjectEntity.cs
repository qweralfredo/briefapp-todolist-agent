namespace BriefappTodoList.Api.Domain;

public class ProjectEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UserId { get; set; } // Firebase UID

    // Configurações de ambiente (IDE/repositório)
    public string? GitHubUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? TechStack { get; set; }
    public string MainBranch { get; set; } = "main";

    // Azure DevOps Sync per-project
    public bool AdoEnabled { get; set; }
    public string? AdoOrganization { get; set; }
    public string? AdoProject { get; set; }
    public string? AdoPat { get; set; }

    public List<BacklogItemEntity> BacklogItems { get; set; } = [];
    public List<SprintEntity> Sprints { get; set; } = [];
    public List<WikiPageEntity> WikiPages { get; set; } = [];
    public List<DocumentationPageEntity> DocumentationPages { get; set; } = [];
    public List<KnowledgeCheckpointEntity> KnowledgeCheckpoints { get; set; } = [];
    public List<AgentRunLogEntity> AgentRuns { get; set; } = [];
}

