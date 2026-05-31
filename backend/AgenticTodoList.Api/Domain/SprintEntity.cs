namespace BriefappTodoList.Api.Domain;

public class SprintEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planned;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> CommitIds { get; set; } = [];

    public List<WorkItemEntity> WorkItems { get; set; } = [];
    public List<ReviewEntity> Reviews { get; set; } = [];
}

