namespace BriefappTodoList.Api.Domain;

// ST-42/44: Model for inbound messages received from OpenClaw webhook
public record InboundMessage(
    string Channel,      // "whatsapp" | "slack" | "telegram"
    string Sender,       // External user ID
    string Message,      // Text content
    string? Attachment,  // Optional file URL
    DateTimeOffset Timestamp
);

// ST-44: Enriched message ready to publish to Tansu
public record RoutedMessage(
    Guid BoxId,
    string UserId,
    ChannelType ChannelType,
    string ExternalSenderId,
    string Content,
    MessageIntent Intent,
    string? AttachmentUrl,
    DateTimeOffset ReceivedAt
);
