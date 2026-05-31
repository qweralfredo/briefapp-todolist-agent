namespace BriefappTodoList.Api.Domain;

public class WorkItemFeedbackEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkItemId { get; set; }
    public WorkItemEntity? WorkItem { get; set; }

    public string AgentName { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string IdeUsed { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
