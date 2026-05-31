namespace BriefappTodoList.Api.Domain;

public enum BoxUserRole
{
    Viewer = 0,
    Editor = 1,
    Admin = 2,
    Owner = 3
}

public class BoxUserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Email { get; set; } = string.Empty;
    public BoxUserRole Role { get; set; } = BoxUserRole.Viewer;
    public string Groups { get; set; } = string.Empty; // CSV
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ProjectEntity? Project { get; set; }
}
