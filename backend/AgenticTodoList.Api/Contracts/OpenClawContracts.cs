namespace BriefappTodoList.Api.Contracts;

// ── ST-45: OpenClaw Registration ──────────────────────────────────────────
public record RegisterChannelUserRequest(
    string ChannelType,  // "whatsapp" | "slack" | "telegram"
    string ExternalId,   // phone number, Slack user ID, or Telegram chat ID
    Guid BoxId
);
