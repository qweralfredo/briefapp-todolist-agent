using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Domain.OpenClaw;

// Note: ChannelType is already defined in BriefappTodoList.Api.Domain (OpenClawEnums.cs)

// ── ST-83: HandoffState ───────────────────────────────────────────────────────

public enum HandoffState { Agent = 0, Human = 1, Transitioning = 2 }

// ── ST-84: ChannelStatus ──────────────────────────────────────────────────────

public enum ChannelStatus { Healthy = 0, Unhealthy = 1, Failover = 2 }

// ── ST-77: Formatter DTOs ─────────────────────────────────────────────────────

public record FormattedMessage(string Content, bool WasTruncated, int OriginalLength, ChannelType Channel);

public record TruncatedResult(string Text, bool WasTruncated, int OriginalLength);

// ── ST-80: OutboundLog ────────────────────────────────────────────────────────

public class OutboundLogEntity
{
    public Guid         Id           { get; set; } = Guid.NewGuid();
    public string       UserId       { get; set; } = string.Empty;
    public ChannelType  Channel      { get; set; }
    public string       Message      { get; set; } = string.Empty;
    public bool         Delivered    { get; set; }
    public string?      ErrorMessage { get; set; }
    public int          RetryCount   { get; set; }
    public DateTimeOffset SentAt     { get; set; } = DateTimeOffset.UtcNow;
}

// ── ST-81: SessionEntity ──────────────────────────────────────────────────────

public class SessionEntity
{
    public Guid          Id            { get; set; } = Guid.NewGuid();
    public string        UserId        { get; set; } = string.Empty;
    public ChannelType   ChannelType   { get; set; }
    public Guid          BoxId         { get; set; }
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>JSON: {language, responseFormat, notifyOnComplete}</summary>
    public string        Preferences   { get; set; } = "{}";
    public HandoffState  HandoffState  { get; set; } = HandoffState.Agent;
    public DateTimeOffset CreatedAt    { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt    { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}

// ── ST-84: ChannelHealthEntity ────────────────────────────────────────────────

public class ChannelHealthEntity
{
    public Guid           Id              { get; set; } = Guid.NewGuid();
    public ChannelType    ChannelType     { get; set; }
    public ChannelStatus  Status          { get; set; } = ChannelStatus.Healthy;
    public DateTimeOffset LastCheckAt     { get; set; } = DateTimeOffset.UtcNow;
    public ChannelType?   FailoverTarget  { get; set; }
    public DateTimeOffset? DownSince      { get; set; }
    public int            CheckCount      { get; set; }
    public int            FailureCount    { get; set; }
    public double         UptimePercent   { get; set; } = 100;
    public double         DeliveryRate    { get; set; } = 100;
    public long           AvgLatencyMs    { get; set; }
}
