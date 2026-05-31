using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using BriefappTodoList.Api.Services;

namespace BriefappTodoList.Api.Tests;

// ============================================================
// BOX4: OpenClaw Unit & Integration Tests (ST-42/44/45/46)
// ============================================================

/// <summary>
/// Unit tests for InboundRouterService — intent detection and user mapping.
/// Uses InMemory DB, no external dependencies required.
/// </summary>
public class InboundRouterServiceTests : IDisposable
{
    private readonly TestAppFactory _factory;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _db;
    private readonly InboundRouterService _router;

    public InboundRouterServiceTests()
    {
        _factory = new TestAppFactory();
        _factory.WithOpenClawEnabled();
        _scope = _factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _router = _scope.ServiceProvider.GetRequiredService<InboundRouterService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _factory.Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<ProjectEntity> CreateBox(string name = "Test Box")
    {
        var box = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "test",
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Projects.Add(box);
        await _db.SaveChangesAsync();
        return box;
    }

    private async Task<UserChannelMapEntity> RegisterUser(Guid boxId, ChannelType channel, string externalId)
    {
        var mapping = new UserChannelMapEntity
        {
            BoxId = boxId,
            ChannelType = channel,
            ExternalId = externalId,
        };
        _db.UserChannelMaps.Add(mapping);
        await _db.SaveChangesAsync();
        return mapping;
    }

    // ── Intent Detection ────────────────────────────────────────────────────

    [Theory]
    [InlineData("/status", MessageIntent.SprintStatus)]
    [InlineData("status", MessageIntent.SprintStatus)]
    [InlineData("Status", MessageIntent.SprintStatus)]
    [InlineData("/STATUS", MessageIntent.SprintStatus)]
    public async Task RouteAsync_StatusKeywords_DetectSprintStatusIntent(string msg, MessageIntent expected)
    {
        var box = await CreateBox();
        await RegisterUser(box.Id, ChannelType.WhatsApp, "+5511999999999");

        var inbound = new InboundMessage("whatsapp", "+5511999999999", msg, null, DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Intent);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("help")]
    [InlineData("ajuda")]
    public async Task RouteAsync_HelpKeywords_DetectHelpIntent(string msg)
    {
        var box = await CreateBox();
        await RegisterUser(box.Id, ChannelType.Telegram, "123456789");

        var inbound = new InboundMessage("telegram", "123456789", msg, null, DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.NotNull(result);
        Assert.Equal(MessageIntent.Help, result.Intent);
    }

    [Theory]
    [InlineData("Quero uma nova feature de login")]
    [InlineData("implementar dashboard")]
    [InlineData("bug no relatório")]
    public async Task RouteAsync_DefaultMessage_DetectFeatureRequestIntent(string msg)
    {
        var box = await CreateBox();
        await RegisterUser(box.Id, ChannelType.Slack, "U012AB3CD");

        var inbound = new InboundMessage("slack", "U012AB3CD", msg, null, DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.NotNull(result);
        Assert.Equal(MessageIntent.FeatureRequest, result.Intent);
    }

    // ── User Mapping ────────────────────────────────────────────────────────

    [Fact]
    public async Task RouteAsync_RegisteredUser_ReturnsRoutedMessage()
    {
        var box = await CreateBox();
        var mapping = await RegisterUser(box.Id, ChannelType.WhatsApp, "+5511988887777");

        var inbound = new InboundMessage("whatsapp", "+5511988887777", "hello", null, DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.NotNull(result);
        Assert.Equal(box.Id, result.BoxId);
        Assert.Equal(ChannelType.WhatsApp, result.ChannelType);
        Assert.Equal("+5511988887777", result.ExternalSenderId);
        Assert.Equal("hello", result.Content);
    }

    [Fact]
    public async Task RouteAsync_UnregisteredUser_ReturnsNull()
    {
        // No mapping registered for this sender
        var inbound = new InboundMessage("whatsapp", "+5511000000000", "hello", null, DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.Null(result);
    }

    [Fact]
    public async Task RouteAsync_SenderOnDifferentChannel_ReturnsNull()
    {
        var box = await CreateBox();
        await RegisterUser(box.Id, ChannelType.Slack, "U012AB3CD");

        // Same ID but wrong channel type
        var inbound = new InboundMessage("telegram", "U012AB3CD", "hello", null, DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.Null(result);
    }

    [Fact]
    public async Task RouteAsync_WithAttachment_PropagatesAttachmentUrl()
    {
        var box = await CreateBox();
        await RegisterUser(box.Id, ChannelType.WhatsApp, "+5511111111111");

        var inbound = new InboundMessage("whatsapp", "+5511111111111", "veja o arquivo", "https://cdn.example.com/file.pdf", DateTimeOffset.UtcNow);
        var result = await _router.RouteAsync(inbound);

        Assert.NotNull(result);
        Assert.Equal("https://cdn.example.com/file.pdf", result.AttachmentUrl);
    }

    [Fact]
    public async Task RouteAsync_UnknownChannel_ThrowsArgumentException()
    {
        var inbound = new InboundMessage("discord", "user123", "hello", null, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => _router.RouteAsync(inbound));
    }
}

/// <summary>
/// Integration tests for OpenClaw REST endpoints.
/// Uses WebApplicationFactory with InMemory DB.
/// </summary>
public class OpenClawEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;
    private const string ApiKey = "test-api-key-1234";

    public OpenClawEndpointTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.WithOpenClawEnabled();
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Guid> CreateBox(string name = "Test Box")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var box = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            Name = name + Guid.NewGuid(),
            Description = "test",
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(box);
        await db.SaveChangesAsync();
        return box.Id;
    }

    // ── POST /api/openclaw/register ─────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201()
    {
        var boxId = await CreateBox("RegBox1");

        var resp = await _client.PostAsJsonAsync("/api/openclaw/register", new
        {
            channelType = "whatsapp",
            externalId = "+5511999990001",
            boxId,
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("+5511999990001", body.GetProperty("externalId").GetString());
        Assert.Equal("WhatsApp", body.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task Register_DuplicateUser_Returns409()
    {
        var boxId = await CreateBox("RegBox2");
        var payload = new { channelType = "telegram", externalId = "99990002", boxId };

        await _client.PostAsJsonAsync("/api/openclaw/register", payload);
        var resp2 = await _client.PostAsJsonAsync("/api/openclaw/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownBox_Returns404()
    {
        var resp = await _client.PostAsJsonAsync("/api/openclaw/register", new
        {
            channelType = "slack",
            externalId = "U999",
            boxId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidChannelType_Returns400()
    {
        var boxId = await CreateBox("RegBox3");
        var resp = await _client.PostAsJsonAsync("/api/openclaw/register", new
        {
            channelType = "discord",
            externalId = "user123",
            boxId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── GET /api/openclaw/users ─────────────────────────────────────────────

    [Fact]
    public async Task ListUsers_FilterByBoxId_ReturnsOnlyBoxUsers()
    {
        var box1 = await CreateBox("ListBox1");
        var box2 = await CreateBox("ListBox2");

        await _client.PostAsJsonAsync("/api/openclaw/register", new { channelType = "whatsapp", externalId = "+5511000111", boxId = box1 });
        await _client.PostAsJsonAsync("/api/openclaw/register", new { channelType = "telegram", externalId = "222333", boxId = box2 });

        var resp = await _client.GetAsync($"/api/openclaw/users?boxId={box1}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var users = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(users.GetArrayLength() >= 1);
        Assert.All(
            Enumerable.Range(0, users.GetArrayLength()).Select(i => users[i]),
            u => Assert.Equal(box1.ToString(), u.GetProperty("boxId").GetString()));
    }

    // ── GET /api/openclaw/stats ─────────────────────────────────────────────

    [Fact]
    public async Task Stats_Returns200WithRegisteredCount()
    {
        var resp = await _client.GetAsync("/api/openclaw/stats");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("registeredUsers", out _));
        Assert.True(body.TryGetProperty("byChannel", out _));
    }

    // ── POST /api/openclaw/webhook ──────────────────────────────────────────

    [Fact]
    public async Task Webhook_WithoutSecret_Returns200()
    {
        // When OpenClaw:WebhookSecret is empty, HMAC check is skipped
        var payload = JsonSerializer.Serialize(new
        {
            channel = "whatsapp",
            sender = "+5511999000000",
            message = "test",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
        });

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/openclaw/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithValidHmac_Returns200()
    {
        const string secret = "test-hmac-secret";
        var webhookFactory = new TestAppFactory();
        webhookFactory.WithOpenClawEnabled();
        webhookFactory.WithOpenClawSecret(secret);
        var hmacClient = webhookFactory.CreateClient();
        hmacClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        var payloadStr = JsonSerializer.Serialize(new
        {
            channel = "slack",
            sender = "U0TEST",
            message = "hello",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
        });
        var bodyBytes = Encoding.UTF8.GetBytes(payloadStr);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant();

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/openclaw/webhook")
        {
            Content = new StringContent(payloadStr, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-OpenClaw-Signature", sig);

        var resp = await hmacClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        webhookFactory.Dispose();
    }

    [Fact]
    public async Task Webhook_WithInvalidHmac_Returns401()
    {
        const string secret = "correct-secret";
        var webhookFactory = new TestAppFactory();
        webhookFactory.WithOpenClawEnabled();
        webhookFactory.WithOpenClawSecret(secret);
        var hmacClient = webhookFactory.CreateClient();
        hmacClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/openclaw/webhook")
        {
            Content = new StringContent("{\"channel\":\"whatsapp\",\"sender\":\"x\",\"message\":\"y\"}", Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-OpenClaw-Signature", "sha256=invalidsignature");

        var resp = await hmacClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        webhookFactory.Dispose();
    }

    // ── Feature flag disabled ───────────────────────────────────────────────

    [Fact]
    public async Task AllEndpoints_WhenDisabled_Return503()
    {
        var disabledFactory = new TestAppFactory(); // OpenClaw disabled by default
        var disabledClient = disabledFactory.CreateClient();
        disabledClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        var endpoints = new[]
        {
            (HttpMethod.Post, "/api/openclaw/webhook"),
            (HttpMethod.Post, "/api/openclaw/register"),
            (HttpMethod.Get,  "/api/openclaw/users"),
            (HttpMethod.Get,  "/api/openclaw/channels"),
            (HttpMethod.Get,  "/api/openclaw/stats"),
        };

        foreach (var (method, path) in endpoints)
        {
            var req = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post)
                req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = await disabledClient.SendAsync(req);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        }

        disabledFactory.Dispose();
    }
}
