namespace BriefappTodoList.Api.Domain;

public class BoxLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Level { get; set; } = "info"; // info, warn, error, debug
    public string Source { get; set; } = string.Empty; // e.g. "api", "mcp", "rag", "memory"
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty; // JSON or stacktrace
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public ProjectEntity? Project { get; set; }
}
