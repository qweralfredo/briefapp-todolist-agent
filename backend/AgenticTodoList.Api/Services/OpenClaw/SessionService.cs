using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using BriefappTodoList.Api.Domain.OpenClaw;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Services.OpenClaw;

// ── ST-81: SessionService ─────────────────────────────────────────────────────

public sealed class SessionService
{
    private readonly AppDbContext           _db;
    private readonly ILogger<SessionService> _logger;

    public SessionService(AppDbContext db, ILogger<SessionService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GetOrCreate ───────────────────────────────────────────────────────────

    public async Task<SessionEntity> GetOrCreateAsync(
        string userId, ChannelType channel, Guid boxId, CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .Where(s => s.UserId == userId && s.ChannelType == channel && s.BoxId == boxId)
            .OrderByDescending(s => s.LastActiveAt)
            .FirstOrDefaultAsync(ct);

        if (session is not null && session.ExpiresAt > DateTimeOffset.UtcNow)
            return session;

        session = new SessionEntity
        {
            UserId      = userId,
            ChannelType = channel,
            BoxId       = boxId,
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Session created for user {UserId} on {Channel}", userId, channel);
        return session;
    }

    // ── UpdateLastActive ──────────────────────────────────────────────────────

    public async Task UpdateLastActiveAsync(Guid sessionId, CancellationToken ct = default)
    {
        await _db.Sessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActiveAt, DateTimeOffset.UtcNow), ct);
    }

    // ── SetPreferences ────────────────────────────────────────────────────────

    public async Task SetPreferencesAsync(
        Guid sessionId, object preferences, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(preferences);
        await _db.Sessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.Preferences, json)
                 .SetProperty(x => x.LastActiveAt, DateTimeOffset.UtcNow), ct);
    }

    // ── ST-82: GetHistory ─────────────────────────────────────────────────────

    /// <summary>Returns last N messages from Memory items for this session.</summary>
    public async Task<IReadOnlyList<object>> GetConversationHistoryAsync(
        Guid sessionId, int limit = 50, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FindAsync([sessionId], ct);
        if (session is null) return [];

        // Memory-Box stores session messages as MemoryItem:
        // Key = "session:{sessionId}:{role}", Value = message content
        var tag   = $"session:{sessionId}";
        var items = await _db.MemoryItems
            .AsNoTracking()
            .Where(m => m.ProjectId == session.BoxId &&
                        EF.Functions.Like(m.Tags, $"%{tag}%"))
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => (object)new
            {
                Content   = m.Value,
                Role      = m.Key.Contains("human") ? "human" : "agent",
                m.CreatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    // ── ST-83: HandoffManager ─────────────────────────────────────────────────

    public async Task<SessionEntity?> RequestHumanHandoffAsync(
        Guid sessionId, string reason, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FindAsync([sessionId], ct);
        if (session is null) return null;

        session.HandoffState = HandoffState.Transitioning;
        session.LastActiveAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("HUMAN HANDOFF REQUESTED for session {SessionId}. Reason: {Reason}", sessionId, reason);
        // Production: notify admin via OpenClawClient.SendMessage
        return session;
    }

    public async Task<SessionEntity?> AcceptHandoffAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FindAsync([sessionId], ct);
        if (session is null) return null;
        session.HandoffState = HandoffState.Human;
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<SessionEntity?> ReturnToAgentAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FindAsync([sessionId], ct);
        if (session is null) return null;
        session.HandoffState = HandoffState.Agent;
        await _db.SaveChangesAsync(ct);
        return session;
    }

    // ── ExpireSessions ────────────────────────────────────────────────────────

    public async Task ExpireSessionsAsync(CancellationToken ct = default)
    {
        var expired = await _db.Sessions
            .Where(s => s.ExpiresAt < DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(ct);

        if (expired > 0)
            _logger.LogInformation("Expired {Count} sessions.", expired);
    }
}

// ── ST-81: SessionExpiryService (BackgroundService) ───────────────────────────

public sealed class SessionExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<SessionExpiryService> _logger;

    public SessionExpiryService(IServiceScopeFactory s, ILogger<SessionExpiryService> l)
    {
        _scopeFactory = s;
        _logger       = l;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc         = scope.ServiceProvider.GetRequiredService<SessionService>();
                await svc.ExpireSessionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "SessionExpiryService tick failed."); }
        }
    }
}
