namespace BriefappTodoList.Api.Domain;

public class WorkItemEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }

    public Guid BacklogItemId { get; set; }
    public BacklogItemEntity? BacklogItem { get; set; }

    public Guid SprintId { get; set; }
    public SprintEntity? Sprint { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public Guid ResponsavelId { get; set; } = Guid.Empty;
    public int TotalTokensSpent { get; set; }
    public string LastModelUsed { get; set; } = string.Empty;
    public string LastIdeUsed { get; set; } = string.Empty;
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Todo;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public string Branch { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public List<string> CommitIds { get; set; } = [];

    public Guid? ParentWorkItemId { get; set; }
    public WorkItemEntity? ParentWorkItem { get; set; }
    public List<WorkItemEntity> SubTasks { get; set; } = [];

    public List<WorkItemFeedbackEntity> AgentFeedbacks { get; set; } = [];
}

