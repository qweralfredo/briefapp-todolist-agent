namespace BriefappTodoList.Api.Domain;

// ST-45: UserChannelMap entity — maps external channel users to Briefapp Boxes
public class UserChannelMapEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The Briefapp Box (Project) this user belongs to.</summary>
    public Guid BoxId { get; set; }
    public ProjectEntity Box { get; set; } = null!;

    /// <summary>Type of messaging channel (WhatsApp, Slack, Telegram).</summary>
    public ChannelType ChannelType { get; set; }

    /// <summary>
    /// External identifier for the user on the channel.
    /// WhatsApp: phone number (+5511999999999)
    /// Slack: user ID (U012AB3CD)
    /// Telegram: chat ID (123456789)
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}
