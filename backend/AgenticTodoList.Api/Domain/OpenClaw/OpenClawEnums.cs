namespace BriefappTodoList.Api.Domain;

// ST-40/44: Channel type enum — supported messaging platforms
public enum ChannelType
{
    WhatsApp = 1,
    Slack = 2,
    Telegram = 3,
}

// ST-44: Intent categories detected by the InboundRouter
public enum MessageIntent
{
    SprintStatus,   // /status — query current sprint info
    Help,           // /help — list available commands
    FeatureRequest, // default — treat message as a feature/task request
}
