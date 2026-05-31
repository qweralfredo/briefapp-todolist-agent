using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Services;

// ST-44: InboundRouter — classifies intent and routes messages to the correct Box
public class InboundRouterService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InboundRouterService> _logger;
    private readonly OpenClawClient _openclaw;

    public InboundRouterService(
        AppDbContext db,
        ILogger<InboundRouterService> logger,
        OpenClawClient openclaw)
    {
        _db = db;
        _logger = logger;
        _openclaw = openclaw;
    }

    /// <summary>
    /// Routes an inbound message:
    /// 1. Lookup sender → UserChannelMap
    /// 2. If unregistered → send /register instructions and return null
    /// 3. Detect intent (regex/keyword)
    /// 4. Return RoutedMessage ready for task queue
    /// </summary>
    public async Task<RoutedMessage?> RouteAsync(InboundMessage msg, CancellationToken ct = default)
    {
        var channelType = ParseChannelType(msg.Channel);

        // ST-44: lookup registered user by (channelType, externalId)
        var mapping = await _db.UserChannelMaps
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.ChannelType == channelType &&
                m.ExternalId == msg.Sender, ct);

        if (mapping is null)
        {
            _logger.LogInformation("Unregistered sender {Sender} on {Channel}. Sending registration prompt.", msg.Sender, msg.Channel);
            try
            {
                await _openclaw.SendMessageAsync(
                    channelType, msg.Sender,
                    "Ola! Voce nao esta registrado. Para vincular sua conta a um Box Briefapp, " +
                    "use o comando: /register {boxId}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to send registration prompt: {Msg}", ex.Message);
            }
            return null;
        }

        var intent = DetectIntent(msg.Message);

        return new RoutedMessage(
            BoxId: mapping.BoxId,
            UserId: mapping.Id.ToString(),
            ChannelType: channelType,
            ExternalSenderId: msg.Sender,
            Content: msg.Message,
            Intent: intent,
            AttachmentUrl: msg.Attachment,
            ReceivedAt: msg.Timestamp
        );
    }

    private static MessageIntent DetectIntent(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        if (text.StartsWith("/status") || text.StartsWith("status"))
            return MessageIntent.SprintStatus;
        if (text.StartsWith("/help") || text == "help" || text == "ajuda")
            return MessageIntent.Help;
        return MessageIntent.FeatureRequest;
    }

    private static ChannelType ParseChannelType(string channel) =>
        channel.ToLowerInvariant() switch
        {
            "whatsapp" => ChannelType.WhatsApp,
            "slack"    => ChannelType.Slack,
            "telegram" => ChannelType.Telegram,
            _          => throw new ArgumentException($"Unknown channel: {channel}")
        };
}
