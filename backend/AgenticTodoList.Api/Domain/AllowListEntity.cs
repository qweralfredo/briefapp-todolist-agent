namespace BriefappTodoList.Api.Domain;

public class AllowListEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty; // CSV: read,write,admin
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProjectEntity? Project { get; set; }
}
