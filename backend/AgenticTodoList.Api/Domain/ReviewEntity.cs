namespace BriefappTodoList.Api.Domain;

public class ReviewEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SprintId { get; set; }
    public SprintEntity? Sprint { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

