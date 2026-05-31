namespace BriefappTodoList.Api.Domain;

public class BacklogItemEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StoryPoints { get; set; }
    public int Priority { get; set; }
    public BacklogItemStatus Status { get; set; } = BacklogItemStatus.New;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Tags { get; set; } = string.Empty;
    public string WikiRefs { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;
    public List<string> CommitIds { get; set; } = [];

    public List<WorkItemEntity> WorkItems { get; set; } = [];
}

