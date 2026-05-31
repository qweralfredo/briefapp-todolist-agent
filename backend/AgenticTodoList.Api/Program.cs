using BriefappTodoList.Api.Contracts;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using BriefappTodoList.Api.Domain.Queue;
using BriefappTodoList.Api.Domain.Sandbox;
using BriefappTodoList.Api.Domain.CircuitBreaker;
using BriefappTodoList.Api.Domain.Budget;
using BriefappTodoList.Api.Services;
using BriefappTodoList.Api.Services.AzureDevOps;
using BriefappTodoList.Api.Services.Queue;
using BriefappTodoList.Api.Services.Sandbox;
using BriefappTodoList.Api.Services.CircuitBreaker;
using BriefappTodoList.Api.Services.Fallback;
using BriefappTodoList.Api.Services.Budget;
using BriefappTodoList.Api.Services.RateLimit;
using BriefappTodoList.Api.Services.OpenClaw;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?? builder.Configuration["FRONTEND_ORIGINS"]
    ?? "http://localhost:8400";
var allowedCorsOrigins = corsOrigins
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddCors(options =>
    options.AddPolicy("FrontendCors", policy =>
        policy.WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://securetoken.google.com/seniordev-portfolio-84g5f";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/seniordev-portfolio-84g5f",
            ValidateAudience = true,
            ValidAudience = "seniordev-portfolio-84g5f",
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddScoped<BriefappTodoList.Api.Services.Agentic.IGeminiMdWriter, BriefappTodoList.Api.Services.Agentic.GeminiMdWriter>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.Agentic.ISubagentProcessManager, BriefappTodoList.Api.Services.Agentic.SubagentProcessManager>();
builder.Services.AddScoped<ScrumService>();



// Real-time metrics (BL-13 SP-10)
builder.Services.AddSingleton<MetricsEventService>();

// Auth & RBAC (BL-14 SP-11)
builder.Services.AddSingleton<ApiKeyService>();



// Azure DevOps Sync (Briefapp Kanban → Azure Boards)
// Always registered since configuration is now per-project
builder.Services.AddHttpClient("azuredevops", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<AzureDevOpsSyncService>();
builder.Services.AddSingleton<AzureDevOpsSyncWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AzureDevOpsSyncWorker>());
// Auto-sync: background queue for real-time ADO sync on writes
builder.Services.AddSingleton<AdoAutoSyncService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AdoAutoSyncService>());

// â”€â”€ BOX1: Sandbox Engine (ST-14) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var sandboxEnabled = builder.Configuration.GetValue<bool>("Sandbox:Enabled", true);
if (sandboxEnabled)
{
    builder.Services.AddScoped<IDockerRuntime, DockerCliRuntime>();
    builder.Services.AddScoped<SandboxService>();
    builder.Services.AddScoped<SandboxLifecycleService>();
    builder.Services.AddHostedService<SandboxGarbageCollector>();

    // BOX1-02: File System Bridge, Network Policy & Metrics (SP-BOX1-02)
    builder.Services.AddScoped<SandboxWorkspaceService>();
    builder.Services.AddScoped<SandboxNetworkPolicyService>();
    builder.Services.AddScoped<SandboxMetricsService>();
    builder.Services.AddHostedService<WorkspaceCleanupService>();
    builder.Services.AddHostedService<SandboxMetricsCollectorService>();
}

// ST-43: OpenClaw integration â€” BOX4 (SP-BOX4-01)
builder.Services.AddHttpClient("openclaw", c =>
{
    var baseUrl = builder.Configuration["OpenClaw:BaseUrl"] ?? "http://localhost:9700";
    c.BaseAddress = new Uri(baseUrl);
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<OpenClawClient>();
builder.Services.AddScoped<InboundRouterService>();

// â”€â”€ BOX2: Transactional Queue â€” Tansu.io + Lock Protocol (ST-15 to ST-30) â”€â”€â”€
builder.Services.AddHttpClient("tansu", c =>
{
    var host = builder.Configuration["Tansu:Host"] ?? "http://localhost";
    var port = builder.Configuration["Tansu:Port"] ?? "9600";
    c.BaseAddress = new Uri($"{host}:{port}");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<BriefappTodoList.Api.Services.Queue.ITansuClient,
                            BriefappTodoList.Api.Services.Queue.TansuClient>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.Queue.TansuPublisherService>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.Queue.LockService>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.Queue.AckService>();
builder.Services.AddHostedService<BriefappTodoList.Api.Services.Queue.LockExpiryService>();
// BOX2-02: DLQ & Dashboard (SP-BOX2-02)
builder.Services.AddScoped<BriefappTodoList.Api.Services.Queue.DeadLetterQueueService>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.Queue.QueueDashboardService>();

// â”€â”€ BOX3: Circuit Breaker Engine (ST-49 / ST-50 / ST-51) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddScoped<ICircuitBreakerService, CircuitBreakerService>();
builder.Services.AddScoped<FailureDetectionService>();

// â”€â”€ BOX3-02: Fallback Strategy Manager (ST-68 / ST-69 / ST-70) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddScoped<IFallbackStrategy, RetryWithLowerTemperatureStrategy>();
builder.Services.AddScoped<IFallbackStrategy, SwapToLargerModelStrategy>();
builder.Services.AddScoped<IFallbackStrategy, SwapToSmallerModelStrategy>();
builder.Services.AddScoped<IFallbackStrategy, SimplifyPromptStrategy>();
builder.Services.AddScoped<IFallbackStrategy, NotifyHumanStrategy>();
builder.Services.AddScoped<FallbackChainExecutor>();

// ST-70: Gemini -> Ollama Fallback Engine
builder.Services.AddHttpClient<OllamaLocalService>(c =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    c.BaseAddress = new Uri(baseUrl);
    c.Timeout = TimeSpan.FromSeconds(60); // LLMs take time
});
builder.Services.AddGeminiOllamaResilience();

// â”€â”€ BOX3-02: Rate Limit Controller (ST-71 / ST-72 / ST-73) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSingleton<RateLimiterService>();

// â”€â”€ BOX3-02: Cost Guard & Token Budget (ST-74 / ST-75 / ST-76) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddScoped<ICostGuardService, CostGuardService>();

// â”€â”€ BOX4-02: Formatter, Sessions & Health (ST-77..86) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddScoped<IChannelFormatter, WhatsAppFormatter>();
builder.Services.AddScoped<IChannelFormatter, SlackFormatter>();
builder.Services.AddScoped<IChannelFormatter, TelegramFormatter>();
builder.Services.AddSingleton<OutboundTemplateEngine>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddHostedService<SessionExpiryService>();
builder.Services.AddSingleton<MessageRetryQueue>();
builder.Services.AddHostedService<RetryWorkerService>();
if (builder.Configuration.GetValue<bool>("OpenClaw:HealthCheckEnabled", true))
    builder.Services.AddHostedService<ChannelHealthService>();

// â”€â”€ BOX5: Prompt Caching Engine (ST-88) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSingleton<BriefappTodoList.Api.Services.PromptCache.PromptCacheConfig>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.PromptCache.IPromptCacheService, BriefappTodoList.Api.Services.PromptCache.PromptCacheService>();

// â”€â”€ BOX5: Provider Cache Adapters (ST-89) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddScoped<BriefappTodoList.Api.Services.PromptCache.Adapters.IProviderCacheAdapter, BriefappTodoList.Api.Services.PromptCache.Adapters.AnthropicCacheAdapter>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.PromptCache.Adapters.IProviderCacheAdapter, BriefappTodoList.Api.Services.PromptCache.Adapters.OpenAiCacheAdapter>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.PromptCache.Adapters.IProviderCacheAdapter, BriefappTodoList.Api.Services.PromptCache.Adapters.GeminiCacheAdapter>();
builder.Services.AddScoped<BriefappTodoList.Api.Services.PromptCache.Adapters.ProviderCacheAdapterFactory>();

// ── BOX5: Cache Metrics Background Worker (ST-90) ───────────────────────────
builder.Services.AddHostedService<BriefappTodoList.Api.Services.PromptCache.PromptCacheMetricsService>();
builder.Services.AddHostedService<AgentMonitorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("FrontendCors");

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var config = context.RequestServices.GetRequiredService<IConfiguration>();
    var mode = config["MODE"] ?? Environment.GetEnvironmentVariable("MODE");
    if (mode == "dev")
    {
        var claims = new[] { 
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-user"),
            new System.Security.Claims.Claim("user_id", "dev-user")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);
    }

    var key = context.Request.Headers["X-Briefapp-Api-Key"].FirstOrDefault();
    if (!string.IsNullOrEmpty(key))
    {
        if (key.StartsWith("pbx_"))
        {
            var db = context.RequestServices.GetRequiredService<BriefappTodoList.Api.Data.AppDbContext>();
            var keyHash = BriefappTodoList.Api.Domain.BoxApiKeyEntity.HashKey(key);
            var entity = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.BoxApiKeys, k => k.KeyHash == keyHash && !k.IsRevoked);

            if (entity != null)
            {
                if (entity.ExpiresAt == null || entity.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    entity.LastUsedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync();

                    var claims = new[] { 
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "api-key-agent"),
                        new System.Security.Claims.Claim("box_id", entity.ProjectId.ToString()),
                        new System.Security.Claims.Claim("scopes", entity.Scopes)
                    };
                    var identity = new System.Security.Claims.ClaimsIdentity(claims, "ApiKey");
                    context.User = new System.Security.Claims.ClaimsPrincipal(identity);
                }
            }
        }
        else
        {
            var apiKeys = context.RequestServices.GetRequiredService<BriefappTodoList.Api.Services.ApiKeyService>();
            if (apiKeys.IsValid(key))
            {
                var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "api-key-agent") };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "ApiKey");
                context.User = new System.Security.Claims.ClaimsPrincipal(identity);
            }
        }
    }
    await next();
});

app.UseAuthorization();

app.Use(async (context, next) =>
{
    var endpoint = context.GetEndpoint();
    if (endpoint != null)
    {
        var routeValues = context.Request.RouteValues;
        Guid? targetProjectId = null;
        var db = context.RequestServices.GetRequiredService<BriefappTodoList.Api.Data.AppDbContext>();

        if (routeValues.TryGetValue("projectId", out var pidObj) && Guid.TryParse(pidObj?.ToString(), out var pid))
            targetProjectId = pid;
        else if (routeValues.TryGetValue("boxId", out var bidObj) && Guid.TryParse(bidObj?.ToString(), out var bid))
            targetProjectId = bid;
        else if (routeValues.TryGetValue("workItemId", out var widObj) && Guid.TryParse(widObj?.ToString(), out var wid))
        {
            var wi = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.WorkItems.AsNoTracking(), w => w.Id == wid);
            if (wi != null) targetProjectId = wi.ProjectId;
        }
        else if (routeValues.TryGetValue("backlogItemId", out var backObj) && Guid.TryParse(backObj?.ToString(), out var backId))
        {
            var bi = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.BacklogItems.AsNoTracking(), b => b.Id == backId);
            if (bi != null) targetProjectId = bi.ProjectId;
        }
        else if (routeValues.TryGetValue("sprintId", out var spObj) && Guid.TryParse(spObj?.ToString(), out var spId))
        {
            var sp = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Sprints.AsNoTracking(), s => s.Id == spId);
            if (sp != null) targetProjectId = sp.ProjectId;
        }

        if (targetProjectId.HasValue)
        {
            var user = context.User;
            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("user_id")?.Value;
            var boxIdStr = user.FindFirst("box_id")?.Value;
            var hasBoxId = Guid.TryParse(boxIdStr, out var boxId);

            var project = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Projects.AsNoTracking(), p => p.Id == targetProjectId.Value);
            
            if (project != null)
            {
                bool isOwner = string.IsNullOrEmpty(project.UserId) || project.UserId == userId;
                bool isBoxKey = hasBoxId && targetProjectId.Value == boxId;

                // Enforce that to access this project, the user must be the owner OR must be authenticated with this project's API Key
                var config = context.RequestServices.GetRequiredService<IConfiguration>();
                var mode = config["MODE"] ?? Environment.GetEnvironmentVariable("MODE");
                if (!isOwner && !isBoxKey && mode != "dev")
                {
                    context.Response.StatusCode = 403;
                    await Microsoft.AspNetCore.Http.HttpResponseJsonExtensions.WriteAsJsonAsync(context.Response, new { error = "Access denied. You do not have permission to access this project's data. A valid API Key for this Box is required." });
                    return;
                }
            }
        }
    }
    await next();
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

app.MapGet("/api/projects", async (bool? includeArchived, AppDbContext db, System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) => {
    var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("user_id")?.Value;
    var boxIdStr = user.FindFirst("box_id")?.Value;
    var hasBoxId = Guid.TryParse(boxIdStr, out var boxId);

    return await (includeArchived == true ? db.Projects : db.Projects.Where(p => p.Status == ProjectStatus.Active))
        .Where(p => string.IsNullOrEmpty(p.UserId) || p.UserId == userId || (hasBoxId && p.Id == boxId))
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Status,
            p.ArchivedAt,
            p.CreatedAt,
            p.GitHubUrl,
            p.LocalPath,
            p.TechStack,
            p.MainBranch,
            p.AdoEnabled,
            p.AdoOrganization,
            p.AdoProject,
            p.AdoPat,
            BacklogCount = p.BacklogItems.Count,
            SprintCount = p.Sprints.Count,
            WikiCount = p.WikiPages.Count,
            DocCount = p.DocumentationPages.Count,
            CheckpointCount = p.KnowledgeCheckpoints.Count,
            AgentRunCount = p.AgentRuns.Count,
        })
        .ToListAsync(ct);
}).RequireAuthorization();

app.MapPost("/api/projects", async (CreateProjectRequest request, ScrumService service, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
{
    var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("user_id")?.Value;
    var project = await service.CreateProjectAsync(request, ct);
    if (!string.IsNullOrEmpty(userId))
    {
        project.UserId = userId;
        await db.SaveChangesAsync(ct);
    }
    return Results.Created($"/api/projects/{project.Id}", project);
}).RequireAuthorization();

app.MapPatch("/api/projects/{projectId:guid}/config", async (Guid projectId, UpdateProjectConfigRequest request, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
    if (project is null)
        return Results.NotFound(new { error = "Project not found." });

    if (request.GitHubUrl is not null) project.GitHubUrl = request.GitHubUrl.Trim();
    if (request.LocalPath is not null) project.LocalPath = request.LocalPath.Trim();
    if (request.TechStack is not null) project.TechStack = request.TechStack.Trim();
    if (request.MainBranch is not null) project.MainBranch = request.MainBranch.Trim();
    
    if (request.AdoEnabled.HasValue) project.AdoEnabled = request.AdoEnabled.Value;
    if (request.AdoOrganization is not null) project.AdoOrganization = request.AdoOrganization.Trim();
    if (request.AdoProject is not null) project.AdoProject = request.AdoProject.Trim();
    if (request.AdoPat is not null) project.AdoPat = request.AdoPat.Trim();

    await db.SaveChangesAsync(ct);
    return Results.Ok(project);
});

app.MapDelete("/api/projects/{projectId:guid}", async (Guid projectId, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
    if (project is null)
    {
        return Results.NotFound(new { error = "Project not found." });
    }

    var result = await ArchiveProjectAsync(db, project, ct);
    return Results.Ok(result);
});

app.MapGet("/api/projects/{projectId:guid}/dashboard", async (Guid projectId, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await service.GetDashboardAsync(projectId, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapDelete("/api/projects/{projectId:guid}/ado/clear", async (Guid projectId, AppDbContext db, AzureDevOpsSyncService ado, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
    if (project is null || !project.AdoEnabled)
        return Results.NotFound(new { error = "Project not found or ADO disabled." });

    await ado.ClearProjectBoardAsync(project, ct);
    return Results.NoContent();
});

app.MapGet("/api/projects/{projectId:guid}/backlog", async (Guid projectId, AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.BacklogItems.Where(b => b.ProjectId == projectId).OrderBy(b => b.Priority).ToListAsync(ct)));

app.MapPost("/api/projects/{projectId:guid}/backlog", async (Guid projectId, AddBacklogItemRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Created($"/api/projects/{projectId}/backlog", await service.AddBacklogItemAsync(projectId, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/projects/{projectId:guid}/sprints", async (Guid projectId, AppDbContext db, CancellationToken ct) =>
{
    var sprints = await db.Sprints
        .Where(s => s.ProjectId == projectId)
        .OrderByDescending(s => s.StartDate)
        .Select(s => new
        {
            s.Id,
            s.Name,
            s.Goal,
            s.StartDate,
            s.EndDate,
            s.Status,
            s.CommitIds,
            WorkItems = db.WorkItems
                .Where(w => w.SprintId == s.Id)
                .OrderBy(w => w.CreatedAt)
                .Select(w => new
                {
                    w.Id,
                    w.BacklogItemId,
                    w.ParentWorkItemId,
                    w.Title,
                    w.Description,
                    w.Assignee,
                    w.TotalTokensSpent,
                    w.LastModelUsed,
                    w.LastIdeUsed,
                    w.Status,
                    w.Branch,
                    w.Tags,
                    w.CommitIds,
                    w.CreatedAt,
                    w.UpdatedAt,
                    Feedbacks = db.WorkItemFeedbacks
                        .Where(f => f.WorkItemId == w.Id)
                        .OrderByDescending(f => f.CreatedAt)
                        .Select(f => new
                        {
                            f.Id,
                            f.AgentName,
                            f.ModelUsed,
                            f.IdeUsed,
                            f.TokensUsed,
                            f.Feedback,
                            f.MetadataJson,
                            f.CreatedAt
                        })
                        .ToList()
                })
                .ToList()
        })
        .ToListAsync(ct);

    return Results.Ok(sprints);
});

app.MapPost("/api/projects/{projectId:guid}/sprints", async (Guid projectId, CreateSprintRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        var sprint = await service.CreateSprintAsync(projectId, request, ct);
        return Results.Created(
            $"/api/projects/{projectId}/sprints/{sprint.Id}",
            new
            {
                sprint.Id,
                sprint.ProjectId,
                sprint.Name,
                sprint.Goal,
                sprint.StartDate,
                sprint.EndDate,
                sprint.Status,
                sprint.CreatedAt
            });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/work-items/{workItemId:guid}/status", async (Guid workItemId, BriefappTodoList.Api.Data.AppDbContext db, CancellationToken ct) =>
{
    try
    {
        var workItem = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.WorkItems.AsNoTracking(), w => w.Id == workItemId, ct);
        if (workItem is null)
        {
            return Results.NotFound(new { error = $"Work item {workItemId} not found." });
        }

        return Results.Ok(new
        {
            workItem.Id,
            workItem.ProjectId,
            workItem.SprintId,
            workItem.BacklogItemId,
            workItem.ParentWorkItemId,
            workItem.Title,
            workItem.Description,
            workItem.Assignee,
            workItem.Status,
            workItem.Branch,
            workItem.Tags,
            workItem.CommitIds,
            workItem.TotalTokensSpent,
            workItem.LastModelUsed,
            workItem.LastIdeUsed,
            workItem.CreatedAt,
            workItem.UpdatedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/work-items/{workItemId:guid}/status", async (Guid workItemId, UpdateWorkItemStatusRequest request, ScrumService service, IServiceProvider sp, CancellationToken ct) =>
{
    try
    {
        var workItem = await service.UpdateWorkItemStatusAsync(workItemId, request, ct);

        // Auto-sync to ADO if configured
        sp.GetService<AdoAutoSyncService>()?.EnqueueWorkItemSync(workItemId);

        return Results.Ok(new
        {
            workItem.Id,
            workItem.ProjectId,
            workItem.SprintId,
            workItem.BacklogItemId,
            workItem.ParentWorkItemId,
            workItem.Title,
            workItem.Description,
            workItem.Assignee,
            workItem.Status,
            workItem.Branch,
            workItem.Tags,
            workItem.CommitIds,
            workItem.TotalTokensSpent,
            workItem.LastModelUsed,
            workItem.LastIdeUsed,
            workItem.CreatedAt,
            workItem.UpdatedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/projects/{projectId:guid}/workitems", async (Guid projectId, Guid sprintId, AddWorkItemRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        var workItem = await service.AddWorkItemAsync(projectId, sprintId, request, ct);
        return Results.Created($"/api/work-items/{workItem.Id}", new
        {
            workItem.Id,
            workItem.ProjectId,
            workItem.SprintId,
            workItem.BacklogItemId,
            workItem.Title,
            workItem.Description,
            workItem.Assignee,
            workItem.Status,
            workItem.Branch,
            workItem.Tags,
            workItem.CommitIds,
            workItem.CreatedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/work-items/{workItemId:guid}/sub-tasks", async (Guid workItemId, AddSubTaskRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        var subTask = await service.AddSubTaskAsync(workItemId, request, ct);
        return Results.Created($"/api/work-items/{workItemId}/sub-tasks/{subTask.Id}", new
        {
            subTask.Id,
            subTask.ProjectId,
            subTask.SprintId,
            subTask.BacklogItemId,
            subTask.ParentWorkItemId,
            subTask.Title,
            subTask.Description,
            subTask.Assignee,
            subTask.Status,
            subTask.Branch,
            subTask.Tags,
            subTask.CommitIds,
            subTask.CreatedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPatch("/api/backlog-items/{backlogItemId:guid}/context", async (Guid backlogItemId, UpdateBacklogItemContextRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await service.UpdateBacklogItemContextAsync(backlogItemId, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapDelete("/api/backlog-items/{backlogItemId:guid}", async (Guid backlogItemId, ScrumService service, CancellationToken ct) =>
{
    try
    {
        await service.DeleteBacklogItemAsync(backlogItemId, ct);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPatch("/api/sprints/{sprintId:guid}/commits", async (Guid sprintId, UpdateSprintCommitIdsRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        var sprint = await service.UpdateSprintCommitIdsAsync(sprintId, request, ct);
        return Results.Ok(new
        {
            sprint.Id,
            sprint.ProjectId,
            sprint.Name,
            sprint.Goal,
            sprint.StartDate,
            sprint.EndDate,
            sprint.Status,
            sprint.CommitIds,
            sprint.CreatedAt
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/sprints/{sprintId:guid}/reviews", async (Guid sprintId, AddReviewRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Created($"/api/sprints/{sprintId}/reviews", await service.AddReviewAsync(sprintId, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/projects/{projectId:guid}/knowledge", async (Guid projectId, AppDbContext db, CancellationToken ct) =>
    Results.Ok(new
    {
        wikiPages = await db.WikiPages.Where(w => w.ProjectId == projectId).OrderByDescending(w => w.UpdatedAt).ToListAsync(ct),
        documentationPages = await db.DocumentationPages.Where(d => d.ProjectId == projectId).OrderBy(d => d.Category).ThenByDescending(d => d.UpdatedAt).ToListAsync(ct),
        checkpoints = await db.KnowledgeCheckpoints.Where(k => k.ProjectId == projectId).OrderByDescending(k => k.CreatedAt).ToListAsync(ct),
        agentRuns = await db.AgentRunLogs.Where(a => a.ProjectId == projectId).OrderByDescending(a => a.StartedAt).Take(100).ToListAsync(ct)
    }));

app.MapPost("/api/projects/{projectId:guid}/wiki", async (Guid projectId, AddWikiPageRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Created($"/api/projects/{projectId}/wiki", await service.AddWikiPageAsync(projectId, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/projects/{projectId:guid}/checkpoints", async (Guid projectId, AddCheckpointRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Created($"/api/projects/{projectId}/checkpoints", await service.AddCheckpointAsync(projectId, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/projects/{projectId:guid}/documentation", async (Guid projectId, AddDocumentationPageRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        return Results.Created($"/api/projects/{projectId}/documentation", await service.AddDocumentationPageAsync(projectId, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/projects/{projectId:guid}/agent-runs", async (Guid projectId, AddAgentRunLogRequest request, ScrumService service, CancellationToken ct) =>
{
    try
    {
        var run = await service.AddAgentRunAsync(projectId, request, ct);
        return Results.Created($"/api/projects/{projectId}/agent-runs", run);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/api/agent-runs/webhook", async (AgentRunWebhookRequest request, ScrumService service, AppDbContext db, CancellationToken ct) =>
{
    try
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Status == ProjectStatus.Active, ct);
        if (project == null) return Results.NotFound("No active project found.");

        var logRequest = new AddAgentRunLogRequest(
            request.AgentName,
            "CLI-Hook",
            "Webhook received",
            request.OutputSummary,
            request.Status,
            request.StartedAt ?? DateTimeOffset.UtcNow,
            request.FinishedAt ?? DateTimeOffset.UtcNow,
            request.ModelName,
            request.TokensInput,
            request.TokensOutput,
            0,
            0,
            request.Success,
            request.ErrorMessage,
            "local"
        );

        var run = await service.AddAgentRunAsync(project.Id, logRequest, ct);
        return Results.Created($"/api/projects/{project.Id}/agent-runs", run);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/agent-runs/{runId:guid}/heartbeat", async (Guid runId, AppDbContext db, CancellationToken ct) =>
{
    var runIdString = runId.ToString();
    var runs = await db.AgentRunLogs.Where(r => r.Id == runId || r.AgentName == runIdString).ToListAsync(ct);
    if (!runs.Any()) return Results.NotFound(new { error = "Agent run not found." });
    
    foreach (var run in runs.Where(r => r.Status != "done" && r.Status != "failed"))
    {
        run.LastActivityAt = DateTimeOffset.UtcNow;
        if (run.Status == "inactive" || run.Status == "stopped") run.Status = "running";
    }
    await db.SaveChangesAsync(ct);
    
    return Results.Ok(new { LastActivityAt = DateTimeOffset.UtcNow });
});

// Global Search (v3)
app.MapGet("/api/search", async (string q, Guid? projectId, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(new SearchResponseDto(q ?? "", 0, new List<SearchResultItemDto>()));

    var query = q.Trim().ToLowerInvariant();
    var results = new List<SearchResultItemDto>();

    // 1. Projects
    var projectsQ = db.Projects.AsNoTracking()
        .Where(p => p.Name.ToLower().Contains(query) || p.Description.ToLower().Contains(query));
    if (projectId.HasValue) projectsQ = projectsQ.Where(p => p.Id == projectId.Value);
    
    var projects = await projectsQ.Take(10).ToListAsync(ct);
    results.AddRange(projects.Select(p => new SearchResultItemDto(
        p.Id.ToString(), "Project", p.Name, p.Description, p.Status.ToString(), p.Id, p.Name, p.CreatedAt)));

    // 2. Backlog Items
    var backlogQ = db.BacklogItems.AsNoTracking().Include(b => b.Project)
        .Where(b => b.Title.ToLower().Contains(query) || b.Description.ToLower().Contains(query));
    if (projectId.HasValue) backlogQ = backlogQ.Where(b => b.ProjectId == projectId.Value);

    var backlog = await backlogQ.Take(20).ToListAsync(ct);
    results.AddRange(backlog.Select(b => new SearchResultItemDto(
        b.Id.ToString(), "BacklogItem", b.Title, b.Description, b.Status.ToString(), b.ProjectId, b.Project?.Name, b.CreatedAt)));

    // 3. Sprints
    var sprintsQ = db.Sprints.AsNoTracking().Include(s => s.Project)
        .Where(s => s.Name.ToLower().Contains(query) || s.Goal.ToLower().Contains(query));
    if (projectId.HasValue) sprintsQ = sprintsQ.Where(s => s.ProjectId == projectId.Value);

    var sprints = await sprintsQ.Take(10).ToListAsync(ct);
    results.AddRange(sprints.Select(s => new SearchResultItemDto(
        s.Id.ToString(), "Sprint", s.Name, s.Goal, s.Status.ToString(), s.ProjectId, s.Project?.Name, s.StartDate.ToDateTime(TimeOnly.MinValue))));

    // 4. Work Items (Tasks)
    var workItemsQ = db.WorkItems.AsNoTracking().Include(w => w.Project)
        .Where(w => w.Title.ToLower().Contains(query) || w.Description.ToLower().Contains(query));
    if (projectId.HasValue) workItemsQ = workItemsQ.Where(w => w.ProjectId == projectId.Value);

    var workItems = await workItemsQ.Take(20).ToListAsync(ct);
    results.AddRange(workItems.Select(w => new SearchResultItemDto(
        w.Id.ToString(), "WorkItem", w.Title, w.Description, w.Status.ToString(), w.ProjectId, w.Project?.Name, w.CreatedAt)));

    // 5. Wiki Pages
    var wikiQ = db.WikiPages.AsNoTracking().Include(w => w.Project)
        .Where(w => w.Title.ToLower().Contains(query) || w.ContentMarkdown.ToLower().Contains(query));
    if (projectId.HasValue) wikiQ = wikiQ.Where(w => w.ProjectId == projectId.Value);

    var wikis = await wikiQ.Take(10).ToListAsync(ct);
    results.AddRange(wikis.Select(w => new SearchResultItemDto(
        w.Id.ToString(), "WikiPage", w.Title, w.Category, null, w.ProjectId, w.Project?.Name, w.UpdatedAt)));

    // 6. Documentation Pages
    var docsQ = db.DocumentationPages.AsNoTracking().Include(d => d.Project)
        .Where(d => d.Title.ToLower().Contains(query) || d.ContentMarkdown.ToLower().Contains(query));
    if (projectId.HasValue) docsQ = docsQ.Where(d => d.ProjectId == projectId.Value);

    var docs = await docsQ.Take(10).ToListAsync(ct);
    results.AddRange(docs.Select(d => new SearchResultItemDto(
        d.Id.ToString(), "DocumentationPage", d.Title, d.Category, null, d.ProjectId, d.Project?.Name, d.UpdatedAt)));

    // 7. Knowledge Checkpoints
    var checkpointsQ = db.KnowledgeCheckpoints.AsNoTracking().Include(k => k.Project)
        .Where(k => k.Name.ToLower().Contains(query) || k.ContextSnapshot.ToLower().Contains(query));
    if (projectId.HasValue) checkpointsQ = checkpointsQ.Where(k => k.ProjectId == projectId.Value);

    var checkpoints = await checkpointsQ.Take(10).ToListAsync(ct);
    results.AddRange(checkpoints.Select(k => new SearchResultItemDto(
        k.Id.ToString(), "KnowledgeCheckpoint", k.Name, k.Category, null, k.ProjectId, k.Project?.Name, k.CreatedAt)));

    var sortedResults = results.OrderByDescending(r => r.CreatedAt).ToList();

    return Results.Ok(new SearchResponseDto(q, sortedResults.Count, sortedResults));
});

// Human Evaluation endpoints (SP-03 BL-06)
app.MapPost("/api/agent-runs/{runId:guid}/evaluations", async (
    Guid runId,
    SubmitHumanEvaluationRequest request,
    AppDbContext db,
    CancellationToken ct) =>
{
    var run = await db.AgentRunLogs.FirstOrDefaultAsync(r => r.Id == runId, ct);
    if (run is null)
        return Results.NotFound(new { error = "Agent run not found." });

    // Composite score: AccuracyÃ—0.30 + RelevanceÃ—0.25 + CompletenessÃ—0.25 + SafetyÃ—0.20, then Ã—5
    var composite = (request.AccuracyScore * 0.30f
                   + request.RelevanceScore * 0.25f
                   + request.CompletenessScore * 0.25f
                   + request.SafetyScore * 0.20f) * 5f;

    var eval = new HumanEvaluationEntity
    {
        AgentRunId = runId,
        ReviewerId = request.ReviewerId,
        AccuracyScore = request.AccuracyScore,
        RelevanceScore = request.RelevanceScore,
        CompletenessScore = request.CompletenessScore,
        SafetyScore = request.SafetyScore,
        Score = MathF.Round(composite, 2),
        FeedbackText = request.FeedbackText,
        RequiresEscalation = request.RequiresEscalation,
        ReviewTimeSeconds = request.ReviewTimeSeconds,
    };

    db.HumanEvaluations.Add(eval);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/agent-runs/{runId}/evaluations/{eval.Id}",
        ToDto(eval));
});

app.MapGet("/api/agent-runs/{runId:guid}/evaluations", async (Guid runId, AppDbContext db, CancellationToken ct) =>
{
    var evals = (await db.HumanEvaluations
        .Where(e => e.AgentRunId == runId)
        .OrderByDescending(e => e.SubmittedAt)
        .ToListAsync(ct))
        .Select(ToDto)
        .ToList();
    return Results.Ok(evals);
});

app.MapGet("/api/projects/{projectId:guid}/evaluations/pending", async (Guid projectId, AppDbContext db, CancellationToken ct) =>
{
    // Work items with agent runs that have no evaluation in last 7 days
    var since = DateTimeOffset.UtcNow.AddDays(-7);
    var pendingRuns = await db.AgentRunLogs
        .Where(r => r.ProjectId == projectId && r.StartedAt >= since)
        .Where(r => !r.HumanEvaluations.Any())
        .OrderByDescending(r => r.StartedAt)
        .Select(r => new { r.Id, r.AgentName, r.EntryPoint, r.StartedAt, r.Success, r.ModelName })
        .ToListAsync(ct);
    return Results.Ok(pendingRuns);
});

// â”€â”€ BL-07: Token & Cost Analytics Pipeline endpoints â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
app.MapGet("/api/projects/{projectId:guid}/metrics/token-summary", async (
    Guid projectId, AppDbContext db, CancellationToken ct,
    int days = 30) =>
{
    var since = DateTimeOffset.UtcNow.AddDays(-days);
    var runs = await db.AgentRunLogs
        .Where(r => r.ProjectId == projectId && r.StartedAt >= since)
        .ToListAsync(ct);

    if (!runs.Any()) return Results.Ok(new
    {
        totalRuns = 0, successRate = 0.0, totalTokensInput = 0, totalTokensOutput = 0,
        totalCostUsd = 0.0, avgCostPerRun = 0.0, avgLatencyMs = 0L,
        byModel = new object[0], dailyRollup = new object[0]
    });

    var byModel = runs
        .GroupBy(r => r.ModelName)
        .Select(g => new
        {
            model = g.Key,
            runs = g.Count(),
            successRate = Math.Round(g.Count(r => r.Success) / (double)g.Count() * 100, 1),
            totalTokens = g.Sum(r => r.TokensInput + r.TokensOutput),
            totalCostUsd = Math.Round((double)g.Sum(r => r.CostUsd), 4),
            avgLatencyMs = (long)g.Average(r => r.LatencyMs)
        }).ToList();

    var dailyRollup = runs
        .GroupBy(r => r.StartedAt.Date)
        .OrderBy(g => g.Key)
        .Select(g => new
        {
            date = g.Key.ToString("yyyy-MM-dd"),
            runs = g.Count(),
            totalCostUsd = Math.Round((double)g.Sum(r => r.CostUsd), 4),
            totalTokens = g.Sum(r => r.TokensInput + r.TokensOutput),
            successRate = Math.Round(g.Count(r => r.Success) / (double)g.Count() * 100, 1)
        }).ToList();

    return Results.Ok(new
    {
        totalRuns = runs.Count,
        successRate = Math.Round(runs.Count(r => r.Success) / (double)runs.Count * 100, 1),
        totalTokensInput = runs.Sum(r => r.TokensInput),
        totalTokensOutput = runs.Sum(r => r.TokensOutput),
        totalCostUsd = Math.Round((double)runs.Sum(r => r.CostUsd), 4),
        avgCostPerRun = Math.Round((double)runs.Average(r => r.CostUsd), 6),
        avgLatencyMs = (long)runs.Average(r => r.LatencyMs),
        byModel,
        dailyRollup
    });
});

app.MapGet("/api/projects/{projectId:guid}/metrics/cost-budget", async (
    Guid projectId, AppDbContext db, IConfiguration cfg, CancellationToken ct) =>
{
    var budgetLimit = cfg.GetValue<double>($"Analytics:BudgetLimitUsd:{projectId}", 100.0);
    var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
    var spentThisMonth = await db.AgentRunLogs
        .Where(r => r.ProjectId == projectId && r.StartedAt >= monthStart)
        .SumAsync(r => r.CostUsd, ct);

    return Results.Ok(new
    {
        budgetUsd = budgetLimit,
        spentUsd = Math.Round((double)spentThisMonth, 4),
        remainingUsd = Math.Round(budgetLimit - (double)spentThisMonth, 4),
        usagePct = Math.Round((double)spentThisMonth / budgetLimit * 100, 1),
        alertLevel = (spentThisMonth / (decimal)budgetLimit) switch
        {
            >= 0.9m => "critical",
            >= 0.7m => "warning",
            _        => "ok"
        }
    });
});

// â”€â”€ BL-17: ML Model Performance endpoints â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
app.MapGet("/api/projects/{projectId:guid}/metrics/model-performance", async (
    Guid projectId, AppDbContext db, CancellationToken ct, int days = 30) =>
{
    var since = DateTimeOffset.UtcNow.AddDays(-days);
    var runs = await db.AgentRunLogs
        .Where(r => r.ProjectId == projectId && r.StartedAt >= since)
        .ToListAsync(ct);

    var models = runs
        .GroupBy(r => r.ModelName)
        .Select(g =>
        {
            var latencies = g.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
            var n = latencies.Count;
            return new
            {
                model          = g.Key,
                totalRuns      = n,
                successRate    = n == 0 ? 0.0 : Math.Round(g.Count(r => r.Success) / (double)n * 100, 1),
                avgLatencyMs   = n == 0 ? 0L : (long)g.Average(r => r.LatencyMs),
                p50LatencyMs   = n == 0 ? 0L : latencies[(int)(n * 0.50)],
                p95LatencyMs   = n == 0 ? 0L : latencies[Math.Min((int)(n * 0.95), n - 1)],
                p99LatencyMs   = n == 0 ? 0L : latencies[Math.Min((int)(n * 0.99), n - 1)],
                avgCostUsd     = n == 0 ? 0.0 : Math.Round((double)g.Average(r => r.CostUsd), 6),
                totalTokens    = g.Sum(r => r.TokensInput + r.TokensOutput)
            };
        })
        .OrderBy(m => m.p50LatencyMs)
        .ToList();

    return Results.Ok(new { projectId, days, models });
});

app.MapGet("/api/projects/{projectId:guid}/metrics/drift", async (
    Guid projectId, AppDbContext db, CancellationToken ct,
    double threshold = 15.0) =>
{
    // Compare last 3 days vs prior 4â€“7 days for latency drift
    var now        = DateTimeOffset.UtcNow;
    var recentFrom = now.AddDays(-3);
    var baseFrom   = now.AddDays(-7);

    var allRuns = await db.AgentRunLogs
        .Where(r => r.ProjectId == projectId && r.StartedAt >= baseFrom)
        .ToListAsync(ct);

    var alerts = allRuns
        .GroupBy(r => r.ModelName)
        .Select(g =>
        {
            var recent   = g.Where(r => r.StartedAt >= recentFrom).ToList();
            var baseline = g.Where(r => r.StartedAt < recentFrom).ToList();
            if (recent.Count == 0 || baseline.Count == 0) return null;

            var recentAvg   = recent.Average(r => r.LatencyMs);
            var baselineAvg = baseline.Average(r => r.LatencyMs);
            if (baselineAvg == 0) return null;

            var driftPct = (recentAvg - baselineAvg) / baselineAvg * 100.0;
            if (driftPct <= threshold) return null;

            return (object)new
            {
                model       = g.Key,
                driftPct    = Math.Round(driftPct, 1),
                recentAvgMs = (long)recentAvg,
                baselineAvgMs = (long)baselineAvg,
                severity    = driftPct > 50 ? "critical" : "warning"
            };
        })
        .Where(a => a is not null)
        .ToList();

    return Results.Ok(new { projectId, thresholdPct = threshold, alerts });
});

// â”€â”€ BL-14: Audit log endpoint (API-key protected) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
app.MapGet("/api/projects/{projectId:guid}/audit-log", async (
    Guid projectId, HttpContext ctx, AppDbContext db,
    ApiKeyService apiKeys, CancellationToken ct) =>
{
    var key = ctx.Request.Headers["X-Briefapp-Api-Key"].FirstOrDefault();
    if (!apiKeys.IsValid(key))
        return Results.Unauthorized();

    var entries = await db.AgentRunLogs
        .Where(r => r.ProjectId == projectId)
        .OrderByDescending(r => r.StartedAt)
        .Take(200)
        .Select(r => new
        {
            r.Id, r.AgentName, r.ModelName, r.StartedAt, r.Success,
            r.TokensInput, r.TokensOutput, r.CostUsd, r.LatencyMs, r.Environment
        })
        .ToListAsync(ct);

    return Results.Ok(new { projectId, count = entries.Count, entries });
});



// â”€â”€ BL-13: SSE endpoint /api/metrics/stream â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
app.MapGet("/api/metrics/stream", async (MetricsEventService events, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    var reader = events.Subscribe();
    try
    {
        // Send connection confirmation
        await ctx.Response.WriteAsync("event: connected\ndata: {\"status\":\"ok\"}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        using var keepAliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var keepAliveTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await keepAliveTimer.WaitForNextTickAsync(ct);
                if (!ct.IsCancellationRequested)
                {
                    await ctx.Response.WriteAsync(": keep-alive\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
        }, ct);

        await foreach (var evt in reader.ReadAllAsync(ct))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(evt.Data);
            await ctx.Response.WriteAsync($"event: {evt.EventType}\ndata: {json}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { /* client disconnected */ }
    finally
    {
        events.Unsubscribe(reader);
    }
});



// ============================================================
// Box Users (v3)
// ============================================================

app.MapGet("/api/boxes/{boxId:guid}/users", async (Guid boxId, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var users = await db.BoxUsers.AsNoTracking()
        .Where(u => u.ProjectId == boxId)
        .OrderBy(u => u.Email)
        .Select(u => new
        {
            u.Id,
            u.ProjectId,
            u.Email,
            Role = u.Role.ToString().ToLowerInvariant(),
            u.Groups,
            u.CreatedAt,
            u.UpdatedAt,
        })
        .ToListAsync(ct);

    return Results.Ok(users);
});

app.MapPost("/api/boxes/{boxId:guid}/users", async (Guid boxId, AddBoxUserRequest request, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var email = request.Email.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest(new { error = "Email is required." });

    var existing = await db.BoxUsers.FirstOrDefaultAsync(u => u.ProjectId == boxId && u.Email == email, ct);
    if (existing is not null)
        return Results.Conflict(new { error = "User already exists in this box." });

    var entity = new BoxUserEntity
    {
        ProjectId = boxId,
        Email = email,
        Role = ParseBoxUserRole(request.Role),
        Groups = request.Groups?.Trim() ?? string.Empty,
    };

    db.BoxUsers.Add(entity);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/boxes/{boxId}/users/{entity.Id}", new
    {
        entity.Id,
        entity.ProjectId,
        entity.Email,
        Role = entity.Role.ToString().ToLowerInvariant(),
        entity.Groups,
        entity.CreatedAt,
        entity.UpdatedAt,
    });
});

app.MapPut("/api/boxes/{boxId:guid}/users/{userId:guid}", async (Guid boxId, Guid userId, UpdateBoxUserRequest request, AppDbContext db, CancellationToken ct) =>
{
    var user = await db.BoxUsers.FirstOrDefaultAsync(u => u.Id == userId && u.ProjectId == boxId, ct);
    if (user is null) return Results.NotFound(new { error = "User not found in this box." });

    if (request.Role is not null) user.Role = ParseBoxUserRole(request.Role);
    if (request.Groups is not null) user.Groups = request.Groups.Trim();
    user.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        user.Id,
        user.ProjectId,
        user.Email,
        Role = user.Role.ToString().ToLowerInvariant(),
        user.Groups,
        user.CreatedAt,
        user.UpdatedAt,
    });
});

app.MapDelete("/api/boxes/{boxId:guid}/users/{userId:guid}", async (Guid boxId, Guid userId, AppDbContext db, CancellationToken ct) =>
{
    var user = await db.BoxUsers.FirstOrDefaultAsync(u => u.Id == userId && u.ProjectId == boxId, ct);
    if (user is null) return Results.NotFound(new { error = "User not found in this box." });

    db.BoxUsers.Remove(user);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { deleted = true, userId = user.Id, email = user.Email });
});

// ============================================================
// Memory-Box (v3)
// ============================================================

app.MapGet("/api/boxes/{boxId:guid}/memory", async (Guid boxId, string? tag, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var query = db.MemoryItems.AsNoTracking().Where(m => m.ProjectId == boxId);

    if (!string.IsNullOrWhiteSpace(tag))
        query = query.Where(m => m.Tags.Contains(tag.Trim()));

    var items = await query
        .OrderBy(m => m.Key)
        .Select(m => new { m.Id, m.ProjectId, m.Key, m.Value, m.Tags, m.CreatedAt, m.UpdatedAt })
        .ToListAsync(ct);

    return Results.Ok(items);
});

app.MapGet("/api/boxes/{boxId:guid}/memory/{key}", async (Guid boxId, string key, AppDbContext db, CancellationToken ct) =>
{
    var item = await db.MemoryItems.AsNoTracking()
        .FirstOrDefaultAsync(m => m.ProjectId == boxId && m.Key == key, ct);

    if (item is null) return Results.NotFound(new { error = "Memory key not found." });

    return Results.Ok(new { item.Id, item.ProjectId, item.Key, item.Value, item.Tags, item.CreatedAt, item.UpdatedAt });
});

app.MapPut("/api/boxes/{boxId:guid}/memory", async (Guid boxId, UpsertMemoryRequest request, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var key = request.Key.Trim();
    if (string.IsNullOrWhiteSpace(key))
        return Results.BadRequest(new { error = "Key is required." });

    var existing = await db.MemoryItems.FirstOrDefaultAsync(m => m.ProjectId == boxId && m.Key == key, ct);

    if (existing is not null)
    {
        existing.Value = request.Value;
        existing.Tags = request.Tags?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }
    else
    {
        existing = new MemoryItemEntity
        {
            ProjectId = boxId,
            Key = key,
            Value = request.Value,
            Tags = request.Tags?.Trim() ?? string.Empty,
        };
        db.MemoryItems.Add(existing);
    }

    await db.SaveChangesAsync(ct);

    return Results.Ok(new { existing.Id, existing.ProjectId, existing.Key, existing.Value, existing.Tags, existing.CreatedAt, existing.UpdatedAt });
});

app.MapDelete("/api/boxes/{boxId:guid}/memory/{key}", async (Guid boxId, string key, AppDbContext db, CancellationToken ct) =>
{
    var item = await db.MemoryItems.FirstOrDefaultAsync(m => m.ProjectId == boxId && m.Key == key, ct);
    if (item is null) return Results.NotFound(new { error = "Memory key not found." });

    db.MemoryItems.Remove(item);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { deleted = true, key = item.Key });
});

// ============================================================
// Box Logs (v3)
// ============================================================

app.MapGet("/api/boxes/{boxId:guid}/logs", async (Guid boxId, string? level, string? source, int? limit, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var query = db.BoxLogs.AsNoTracking().Where(l => l.ProjectId == boxId);

    if (!string.IsNullOrWhiteSpace(level))
        query = query.Where(l => l.Level == level.Trim().ToLowerInvariant());

    if (!string.IsNullOrWhiteSpace(source))
        query = query.Where(l => l.Source.Contains(source.Trim()));

    var take = Math.Clamp(limit ?? 100, 1, 500);

    var logs = await query
        .OrderByDescending(l => l.Timestamp)
        .Take(take)
        .Select(l => new { l.Id, l.ProjectId, l.Level, l.Source, l.Message, l.Details, l.Timestamp })
        .ToListAsync(ct);

    return Results.Ok(logs);
});

app.MapPost("/api/boxes/{boxId:guid}/logs", async (Guid boxId, CreateLogRequest request, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var log = new BoxLogEntity
    {
        ProjectId = boxId,
        Level = (request.Level?.Trim().ToLowerInvariant()) switch
        {
            "warn" or "warning" => "warn",
            "error" => "error",
            "debug" => "debug",
            _ => "info",
        },
        Source = request.Source?.Trim() ?? string.Empty,
        Message = request.Message ?? string.Empty,
        Details = request.Details ?? string.Empty,
    };

    db.BoxLogs.Add(log);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/boxes/{boxId}/logs", new { log.Id, log.ProjectId, log.Level, log.Source, log.Message, log.Details, log.Timestamp });
});

// ============================================================
// Box API Keys (v3)
// ============================================================

app.MapGet("/api/boxes/{boxId:guid}/api-keys", async (Guid boxId, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var keys = await db.BoxApiKeys.AsNoTracking()
        .Where(k => k.ProjectId == boxId)
        .OrderByDescending(k => k.CreatedAt)
        .Select(k => new { k.Id, k.Name, k.Prefix, k.Scopes, k.CreatedAt, k.ExpiresAt, k.LastUsedAt, k.IsRevoked })
        .ToListAsync(ct);

    return Results.Ok(keys);
});

app.MapPost("/api/boxes/{boxId:guid}/api-keys", async (Guid boxId, CreateApiKeyRequest request, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Name is required." });

    var rawKey = BoxApiKeyEntity.GenerateKey();
    var entity = new BoxApiKeyEntity
    {
        ProjectId = boxId,
        Name = request.Name.Trim(),
        Prefix = rawKey[..12],
        KeyHash = BoxApiKeyEntity.HashKey(rawKey),
        Scopes = request.Scopes?.Trim() ?? "read",
    };

    db.BoxApiKeys.Add(entity);
    await db.SaveChangesAsync(ct);

    // Return raw key ONCE â€” never stored
    return Results.Created($"/api/boxes/{boxId}/api-keys", new
    {
        entity.Id,
        entity.Name,
        entity.Prefix,
        entity.Scopes,
        entity.CreatedAt,
        key = rawKey, // only time the full key is shown
    });
});

app.MapDelete("/api/boxes/{boxId:guid}/api-keys/{keyId:guid}", async (Guid boxId, Guid keyId, AppDbContext db, CancellationToken ct) =>
{
    var entity = await db.BoxApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.ProjectId == boxId, ct);
    if (entity is null) return Results.NotFound(new { error = "API key not found." });

    entity.IsRevoked = true;
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { revoked = true, keyId = entity.Id, name = entity.Name });
});

// ============================================================
// Allow-List (v3)
// ============================================================

app.MapGet("/api/boxes/{boxId:guid}/allow-list", async (Guid boxId, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var items = await db.AllowListEntries.AsNoTracking()
        .Where(a => a.ProjectId == boxId)
        .OrderByDescending(a => a.CreatedAt)
        .Select(a => new { a.Id, a.ProjectId, a.AppName, a.CallbackUrl, a.Scopes, a.IsActive, a.CreatedAt })
        .ToListAsync(ct);

    return Results.Ok(items);
});

app.MapPut("/api/boxes/{boxId:guid}/allow-list", async (Guid boxId, UpsertAllowListRequest request, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    if (string.IsNullOrWhiteSpace(request.AppName))
        return Results.BadRequest(new { error = "AppName is required." });

    var appName = request.AppName.Trim();
    var existing = await db.AllowListEntries.FirstOrDefaultAsync(a => a.ProjectId == boxId && a.AppName == appName, ct);

    if (existing is not null)
    {
        existing.CallbackUrl = request.CallbackUrl?.Trim() ?? string.Empty;
        existing.Scopes = request.Scopes?.Trim() ?? "read";
    }
    else
    {
        existing = new AllowListEntity
        {
            ProjectId = boxId,
            AppName = appName,
            CallbackUrl = request.CallbackUrl?.Trim() ?? string.Empty,
            Scopes = request.Scopes?.Trim() ?? "read",
            IsActive = true,
        };
        db.AllowListEntries.Add(existing);
    }

    await db.SaveChangesAsync(ct);

    return Results.Ok(new { existing.Id, existing.ProjectId, existing.AppName, existing.CallbackUrl, existing.Scopes, existing.IsActive, existing.CreatedAt });
});

app.MapPatch("/api/boxes/{boxId:guid}/allow-list/{id:guid}/toggle", async (Guid boxId, Guid id, AppDbContext db, CancellationToken ct) =>
{
    var entity = await db.AllowListEntries.FirstOrDefaultAsync(a => a.Id == id && a.ProjectId == boxId, ct);
    if (entity is null) return Results.NotFound(new { error = "Allow-list entry not found." });

    entity.IsActive = !entity.IsActive;
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { entity.Id, entity.IsActive });
});

app.MapDelete("/api/boxes/{boxId:guid}/allow-list/{id:guid}", async (Guid boxId, Guid id, AppDbContext db, CancellationToken ct) =>
{
    var entity = await db.AllowListEntries.FirstOrDefaultAsync(a => a.Id == id && a.ProjectId == boxId, ct);
    if (entity is null) return Results.NotFound(new { error = "Allow-list entry not found." });

    db.AllowListEntries.Remove(entity);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { deleted = true, id = entity.Id });
});

// ============================================================
// Usage Module (v3)
// ============================================================

app.MapGet("/api/boxes/{boxId:guid}/usage", async (Guid boxId, AppDbContext db, CancellationToken ct) =>
{
    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == boxId, ct);
    if (project is null) return Results.NotFound(new { error = "Box not found." });

    var runs = await db.AgentRunLogs.AsNoTracking()
        .Where(r => r.ProjectId == boxId)
        .ToListAsync(ct);

    var totalRuns = runs.Count;
    var totalTokensInput = runs.Sum(r => r.TokensInput);
    var totalTokensOutput = runs.Sum(r => r.TokensOutput);
    var totalCostUsd = runs.Sum(r => r.CostUsd);
    
    var successfulRuns = runs.Count(r => r.Success);
    var successRate = totalRuns > 0 ? (double)successfulRuns / totalRuns * 100 : 0;

    var runsByModel = runs
        .Where(r => !string.IsNullOrEmpty(r.ModelName))
        .GroupBy(r => r.ModelName)
        .ToDictionary(g => g.Key, g => g.Count());

    var summary = new BoxUsageSummaryDto(
        totalRuns,
        totalTokensInput,
        totalTokensOutput,
        totalCostUsd,
        successRate,
        runsByModel
    );

    return Results.Ok(summary);
});

// ============================================================
// BOX1: Sandbox Engine REST API (ST-05 + ST-13)
// ============================================================

app.MapPost("/api/sandbox", async (
    CreateSandboxRequest req,
    SandboxService svc,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!cfg.GetValue<bool>("Sandbox:Enabled", true))
        return Results.StatusCode(503);

    var config = new SandboxConfig
    {
        ImageName      = req.ImageName,
        CpuCores       = req.CpuCores        ?? 2.0,
        MemoryMb       = req.MemoryMb        ?? 512,
        TimeoutMinutes = req.TimeoutMinutes  ?? 30,
        NetworkMode    = req.NetworkMode     ?? SandboxNetworkMode.Restricted,
        WorkDir        = req.WorkDir         ?? "/app",
    };

    try
    {
        var sandbox = await svc.CreateSandboxAsync(req.BoxId, config, req.TaskId, ct);
        return Results.Created($"/api/sandbox/{sandbox.Id}", sandbox);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/sandbox/{id:guid}", async (
    Guid id,
    SandboxService svc,
    CancellationToken ct) =>
{
    var sandbox = await svc.GetSandboxAsync(id, ct);
    return sandbox is null
        ? Results.NotFound(new { error = "Sandbox not found." })
        : Results.Ok(sandbox);
});

app.MapGet("/api/sandbox", async (
    Guid boxId,
    SandboxService svc,
    CancellationToken ct) =>
    Results.Ok(await svc.GetSandboxesByBoxAsync(boxId, ct)));

app.MapDelete("/api/sandbox/{id:guid}", async (
    Guid id,
    SandboxLifecycleService lifecycle,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!cfg.GetValue<bool>("Sandbox:Enabled", true))
        return Results.StatusCode(503);
    try
    {
        await lifecycle.DestroySandboxAsync(id, ct);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Sandbox not found." });
    }
});

app.MapPost("/api/sandbox/{id:guid}/start", async (
    Guid id,
    SandboxLifecycleService lifecycle,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!cfg.GetValue<bool>("Sandbox:Enabled", true))
        return Results.StatusCode(503);
    try
    {
        var sandbox = await lifecycle.StartSandboxAsync(id, ct);
        return Results.Ok(sandbox);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Sandbox not found." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/sandbox/{id:guid}/stop", async (
    Guid id,
    SandboxLifecycleService lifecycle,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!cfg.GetValue<bool>("Sandbox:Enabled", true))
        return Results.StatusCode(503);
    try
    {
        var sandbox = await lifecycle.StopSandboxAsync(id, ct);
        return Results.Ok(sandbox);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Sandbox not found." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/sandbox/{id:guid}/exec", async (
    Guid id,
    SandboxExecRequest req,
    SandboxLifecycleService lifecycle,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!cfg.GetValue<bool>("Sandbox:Enabled", true))
        return Results.StatusCode(503);
    try
    {
        var result = await lifecycle.ExecInSandboxAsync(
            id, req.Command, req.WorkDir, req.TimeoutSeconds, ct);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Sandbox not found." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// ST-13: Sandbox Stats endpoint
app.MapGet("/api/sandbox/stats", async (
    SandboxService svc,
    CancellationToken ct) =>
    Results.Ok(await svc.GetStatsAsync(ct)));

// ============================================================
// BOX1-02: File System, Network Policy & Metrics (ST-31 to ST-35)
// ============================================================

// ST-31: Prepare workspace (git clone + OverlayFS) for a sandbox
app.MapPost("/api/sandbox/{id:guid}/workspace", async (
    Guid id,
    PrepareWorkspaceRequest req,
    SandboxService svc,
    SandboxWorkspaceService workspaceSvc,
    CancellationToken ct) =>
{
    var sandbox = await svc.GetSandboxAsync(id, ct);
    if (sandbox is null) return Results.NotFound();
    try
    {
        var info = await workspaceSvc.PrepareWorkspaceAsync(
            sandbox.ContainerId, sandbox.TaskId ?? id.ToString(), req.GitRepoUrl, req.Branch ?? "main", ct);
        return Results.Ok(info);
    }
    catch (InvalidOperationException ex)
    { return Results.BadRequest(new { error = ex.Message }); }
});

// ST-32: Delete workspace
app.MapDelete("/api/sandbox/{id:guid}/workspace", async (
    Guid id,
    SandboxService svc,
    SandboxWorkspaceService workspaceSvc,
    CancellationToken ct) =>
{
    var sandbox = await svc.GetSandboxAsync(id, ct);
    if (sandbox is null) return Results.NotFound();
    await workspaceSvc.CleanupWorkspaceAsync(sandbox.TaskId ?? id.ToString(), ct);
    return Results.NoContent();
});

// ST-34: Metrics history
app.MapGet("/api/sandbox/{id:guid}/metrics", async (
    Guid id,
    int? limit,
    SandboxMetricsService metricsSvc,
    CancellationToken ct) =>
{
    var history = await metricsSvc.GetHistoryAsync(id, limit ?? 100, ct);
    return Results.Ok(history);
});

// ST-34: Live metrics SSE stream
app.MapGet("/api/sandbox/{id:guid}/metrics/live", async (
    Guid id,
    HttpResponse response,
    SandboxService svc,
    SandboxMetricsService metricsSvc,
    CancellationToken ct) =>
{
    var sandbox = await svc.GetSandboxAsync(id, ct);
    if (sandbox is null) { response.StatusCode = 404; return; }

    response.Headers["Content-Type"]  = "text/event-stream";
    response.Headers["Cache-Control"] = "no-cache";
    response.Headers["Connection"]    = "keep-alive";

    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
    while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
    {
        var m = await metricsSvc.CollectAndPersistAsync(id, sandbox.ContainerId, ct);
        if (m is null) break;
        var json = System.Text.Json.JsonSerializer.Serialize(m);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
});

// ST-33: Network policy info
app.MapGet("/api/sandbox/networks", () =>
    Results.Ok(new
    {
        policies = Enum.GetNames<SandboxNetworkPolicy>(),
        allowedDomains = SandboxNetworks.AllowedDomains,
        restrictedNetworkName = SandboxNetworks.Restricted,
    }));

// ============================================================
// BOX2: Transactional Queue REST API (ST-17 to ST-30)
// ============================================================

// ST-17: Publish task to queue
app.MapPost("/api/tasks", async (
    PublishTaskRequest req,
    TansuPublisherService publisher,
    CancellationToken ct) =>
{
    var task = await publisher.PublishTaskAsync(
        req.BoxId, req.Payload, req.Source ?? "api", req.WorkItemId,
        req.MaxRetries ?? 3, ct: ct);
    return Results.Created($"/api/tasks/{task.Id}", new
    {
        task.Id, task.BoxId, task.Topic, task.Status,
        task.Source, task.CreatedAt, task.ScheduledAt,
    });
});

// ST-21: Queue status per box
app.MapGet("/api/tasks/status", async (
    Guid? boxId,
    TansuPublisherService publisher,
    CancellationToken ct) =>
{
    var stats = await publisher.GetQueueStatusAsync(boxId, ct);
    return Results.Ok(stats);
});

// ST-19: Get task by ID
app.MapGet("/api/tasks/{id:guid}", async (
    Guid id,
    AppDbContext db,
    CancellationToken ct) =>
{
    var task = await db.TaskMessages.FindAsync([id], ct);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

// â”€â”€ Lock Protocol â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// ST-24: Acquire lock
app.MapPost("/api/locks", async (
    AcquireLockRequest req,
    LockService lockSvc,
    CancellationToken ct) =>
{
    try
    {
        var result = await lockSvc.AcquireLockAsync(req.TaskId, req.WorkerId, req.TimeoutMinutes ?? 30, ct);
        return Results.Created($"/api/locks/{result.LockId}", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

// ST-25: Heartbeat
app.MapPost("/api/locks/{lockId:guid}/heartbeat", async (
    Guid lockId,
    HeartbeatLockRequest req,
    LockService lockSvc,
    CancellationToken ct) =>
{
    var result = await lockSvc.HeartbeatAsync(lockId, req.WorkerId, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// ST-26: Release lock
app.MapDelete("/api/locks/{lockId:guid}", async (
    Guid lockId,
    string? workerId,
    bool? force,
    LockService lockSvc,
    CancellationToken ct) =>
{
    if (force == true)
    {
        await lockSvc.ForceReleaseAsync(lockId, ct);
        return Results.NoContent();
    }
    if (string.IsNullOrEmpty(workerId))
        return Results.BadRequest(new { error = "workerId is required unless force=true" });
    var released = await lockSvc.ReleaseLockAsync(lockId, workerId, ct: ct);
    return released ? Results.NoContent() : Results.NotFound();
});

// ST-28: Lock status
app.MapGet("/api/locks/{taskId}", async (
    string taskId,
    LockService lockSvc,
    CancellationToken ct) =>
{
    var lockEntry = await lockSvc.GetActiveLockAsync(taskId, ct);
    if (lockEntry is null) return Results.Ok(new { locked = false });
    var heartbeatAge = (DateTimeOffset.UtcNow - lockEntry.HeartbeatAt).TotalSeconds;
    return Results.Ok(new
    {
        locked       = true,
        lockId       = lockEntry.Id,
        lockedBy     = lockEntry.WorkerId,
        expiresAt    = lockEntry.ExpiresAt,
        heartbeatAgeSeconds = (int)heartbeatAge,
    });
});

// â”€â”€ ACK / NACK â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// ST-29/30: ACK or NACK a task
app.MapPost("/api/tasks/{taskId}/ack", async (
    string taskId,
    AckPayload ack,
    AckService ackSvc,
    CancellationToken ct) =>
{
    if (ack.TaskId != taskId)
        return Results.BadRequest(new { error = "TaskId mismatch." });

    try
    {
        var result = await ackSvc.ProcessAckAsync(ack, ct);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// ============================================================
// BOX2-02: Dead Letter Queue & Dashboard REST API (ST-36 to ST-38, ST-47)
// ============================================================

// ST-38: List DLQ entries (paged)
app.MapGet("/api/queue/dlq", async (
    Guid?              boxId,
    DlqStatus?         status,
    int?               page,
    int?               size,
    DeadLetterQueueService dlq,
    CancellationToken  ct) =>
{
    var result = await dlq.GetPagedAsync(boxId, status, page ?? 1, size ?? 20, ct);
    return Results.Ok(result);
});

// ST-38: Retry single DLQ entry
app.MapPost("/api/queue/dlq/{id:guid}/retry", async (
    Guid                   id,
    DeadLetterQueueService dlq,
    CancellationToken      ct) =>
{
    var ok = await dlq.RetryFromDlqAsync(id, ct);
    return ok ? Results.Ok(new { dlqId = id, status = "Retrying" })
              : Results.NotFound(new { error = "DLQ entry not found or quarantined." });
});

// ST-38: Quarantine DLQ entry
app.MapPost("/api/queue/dlq/{id:guid}/quarantine", async (
    Guid                   id,
    DeadLetterQueueService dlq,
    CancellationToken      ct) =>
{
    var ok = await dlq.QuarantineEntryAsync(id, ct);
    return ok ? Results.Ok(new { dlqId = id, status = "Quarantined" })
              : Results.NotFound(new { error = "DLQ entry not found." });
});

// ST-38: Delete DLQ entry permanently
app.MapDelete("/api/queue/dlq/{id:guid}", async (
    Guid                   id,
    AppDbContext           db,
    CancellationToken      ct) =>
{
    var entry = await db.DlqEntries.FindAsync([id], ct);
    if (entry is null) return Results.NotFound();
    db.DlqEntries.Remove(entry);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

// ST-38: Drain â€” retry all Pending
app.MapPost("/api/queue/dlq/drain", async (
    Guid?                  boxId,
    DeadLetterQueueService dlq,
    CancellationToken      ct) =>
{
    var count = await dlq.DrainDlqAsync(boxId, ct);
    return Results.Ok(new { resubmitted = count });
});

// ST-38: DLQ stats
app.MapGet("/api/queue/dlq/stats", async (
    Guid?                  boxId,
    DeadLetterQueueService dlq,
    CancellationToken      ct) =>
    Results.Ok(await dlq.GetDlqStatsAsync(boxId, ct)));

// ST-47: Queue dashboard snapshot
app.MapGet("/api/queue/stats", async (
    Guid?                   boxId,
    QueueDashboardService   dashboard,
    CancellationToken       ct) =>
    Results.Ok(await dashboard.GetLiveStatsAsync(boxId, ct)));

// ST-47: Queue dashboard SSE live stream
app.MapGet("/api/queue/stats/live", async (
    Guid?                   boxId,
    HttpResponse            response,
    QueueDashboardService   dashboard,
    CancellationToken       ct) =>
{
    response.Headers["Content-Type"]  = "text/event-stream";
    response.Headers["Cache-Control"] = "no-cache";
    response.Headers["Connection"]    = "keep-alive";

    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
    while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
    {
        var stats = await dashboard.GetLiveStatsAsync(boxId, ct);
        var json  = System.Text.Json.JsonSerializer.Serialize(stats);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
});

// ============================================================
// BOX4: OpenClaw Gateway REST API (ST-42/43/44/45)
// ============================================================


// ST-43: Feature flag check helper
bool OpenClawEnabled(IConfiguration cfg) => cfg.GetValue<bool>("OpenClaw:Enabled", false);

// ST-42: Webhook receiver â€” receives inbound messages from OpenClaw
// Validates HMAC-SHA256 signature, enqueues for async routing.
app.MapPost("/api/openclaw/webhook", async (
    HttpContext ctx,
    IConfiguration cfg,
    InboundRouterService router,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!OpenClawEnabled(cfg))
        return Results.StatusCode(503);

    var secret = cfg["OpenClaw:WebhookSecret"] ?? string.Empty;
    var signatureHeader = ctx.Request.Headers["X-OpenClaw-Signature"].FirstOrDefault() ?? string.Empty;

    // Read body for HMAC verification
    ctx.Request.EnableBuffering();
    using var ms = new System.IO.MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms, ct);
    var bodyBytes = ms.ToArray();
    ctx.Request.Body.Position = 0;

    // Validate HMAC-SHA256 (ST-42)
    if (!string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(signatureHeader))
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var expected = "sha256=" + Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant();
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(signatureHeader)))
        {
            logger.LogWarning("OpenClaw webhook HMAC validation failed from {IP}", ctx.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }
    }

    var payload = System.Text.Json.JsonDocument.Parse(bodyBytes).RootElement;
    var channel   = payload.TryGetProperty("channel",    out var ch) ? ch.GetString()  ?? "" : "";
    var sender    = payload.TryGetProperty("sender",     out var sn) ? sn.GetString()  ?? "" : "";
    var message   = payload.TryGetProperty("message",    out var mg) ? mg.GetString()  ?? "" : "";
    var attachment = payload.TryGetProperty("attachment", out var at) ? at.GetString()       : null;
    var timestamp  = payload.TryGetProperty("timestamp",  out var ts)
        ? DateTimeOffset.TryParse(ts.GetString(), out var dto) ? dto : DateTimeOffset.UtcNow
        : DateTimeOffset.UtcNow;

    var inbound = new InboundMessage(channel, sender, message, attachment, timestamp);

    // Route to Box (async â€” we respond 200 immediately)
    _ = Task.Run(async () =>
    {
        try { await router.RouteAsync(inbound, CancellationToken.None); }
        catch (Exception ex) { logger.LogError(ex, "InboundRouter failed for sender {Sender}", sender); }
    }, CancellationToken.None);

    return Results.Ok(new { received = true });
});

// ST-45: Register a channel user â†’ Box mapping
app.MapPost("/api/openclaw/register", async (
    RegisterChannelUserRequest req,
    AppDbContext db,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!OpenClawEnabled(cfg))
        return Results.StatusCode(503);

    var box = await db.Projects.FirstOrDefaultAsync(p => p.Id == req.BoxId, ct);
    if (box is null) return Results.NotFound(new { error = "Box not found." });

    if (!Enum.TryParse<ChannelType>(req.ChannelType, ignoreCase: true, out var channelType))
        return Results.BadRequest(new { error = $"Unknown channelType '{req.ChannelType}'. Use: whatsapp, slack, telegram." });

    var existing = await db.UserChannelMaps.FirstOrDefaultAsync(
        m => m.ChannelType == channelType && m.ExternalId == req.ExternalId, ct);
    if (existing is not null)
        return Results.Conflict(new { error = "User already registered for this channel.", mappingId = existing.Id });

    var mapping = new UserChannelMapEntity
    {
        BoxId = req.BoxId,
        ChannelType = channelType,
        ExternalId = req.ExternalId.Trim(),
    };
    db.UserChannelMaps.Add(mapping);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/openclaw/users/{mapping.Id}", new
    {
        mapping.Id,
        mapping.BoxId,
        Channel = mapping.ChannelType.ToString(),
        mapping.ExternalId,
        mapping.RegisteredAt,
    });
});

// ST-45: List registered users for a Box
app.MapGet("/api/openclaw/users", async (
    Guid? boxId,
    AppDbContext db,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!OpenClawEnabled(cfg))
        return Results.StatusCode(503);

    var query = db.UserChannelMaps.AsNoTracking();
    if (boxId.HasValue)
        query = query.Where(m => m.BoxId == boxId.Value);

    var users = await query
        .OrderByDescending(m => m.RegisteredAt)
        .Select(m => new
        {
            m.Id,
            m.BoxId,
            Channel = m.ChannelType.ToString(),
            m.ExternalId,
            m.RegisteredAt,
        })
        .ToListAsync(ct);

    return Results.Ok(users);
});

// ST-46: Channel status endpoint
app.MapGet("/api/openclaw/channels", async (
    OpenClawClient client,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!OpenClawEnabled(cfg))
        return Results.StatusCode(503);

    var channels = await client.ListConnectedChannelsAsync(ct);
    return Results.Ok(channels);
});

// ST-46: Inbound message stats
app.MapGet("/api/openclaw/stats", async (
    AppDbContext db,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    if (!OpenClawEnabled(cfg))
        return Results.StatusCode(503);

    var today = DateTimeOffset.UtcNow.Date;
    var byChannel = await db.UserChannelMaps.AsNoTracking()
        .GroupBy(m => m.ChannelType)
        .Select(g => new { channel = g.Key.ToString(), users = g.Count() })
        .ToListAsync(ct);

    return Results.Ok(new
    {
        registeredUsers = await db.UserChannelMaps.CountAsync(ct),
        byChannel,
    });
});



// â”€â”€ BOX3: Circuit Breaker REST Endpoints (ST-52) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/breaker/{boxId} â€” estado atual do circuit breaker
app.MapGet("/api/breaker/{boxId:guid}", async (Guid boxId, ICircuitBreakerService breaker, CancellationToken ct) =>
{
    var cb = await breaker.GetOrCreateAsync(boxId, ct);
    return Results.Ok(cb.ToDto());
});

// GET /api/breaker/all â€” todos os circuit breakers
app.MapGet("/api/breaker/all", async (ICircuitBreakerService breaker, CancellationToken ct) =>
{
    var all = await breaker.GetAllAsync(ct);
    return Results.Ok(all.Select(cb => cb.ToDto()));
});

// POST /api/breaker/{boxId}/reset â€” forÃ§a estado Closed
app.MapPost("/api/breaker/{boxId:guid}/reset", async (Guid boxId, ICircuitBreakerService breaker, CancellationToken ct) =>
{
    await breaker.ResetAsync(boxId, ct);
    var cb = await breaker.GetOrCreateAsync(boxId, ct);
    return Results.Ok(cb.ToDto());
});

// POST /api/breaker/{boxId}/config â€” atualiza threshold/cooldown/halfOpenMaxCalls
app.MapPost("/api/breaker/{boxId:guid}/config", async (
    Guid boxId,
    UpdateBreakerConfigRequest req,
    ICircuitBreakerService breaker,
    CancellationToken ct) =>
{
    await breaker.UpdateConfigAsync(boxId, req.FailureThreshold, req.CooldownSeconds, req.HalfOpenMaxCalls, ct);
    var cb = await breaker.GetOrCreateAsync(boxId, ct);
    return Results.Ok(cb.ToDto());
});

// GET /api/breaker/{boxId}/history?limit=50 â€” Ãºltimas transiÃ§Ãµes
app.MapGet("/api/breaker/{boxId:guid}/history", async (
    Guid boxId,
    ICircuitBreakerService breaker,
    CancellationToken ct,
    int limit = 50) =>
{
    var history = await breaker.GetHistoryAsync(boxId, limit, ct);
    return Results.Ok(history);
});

// â”€â”€ BOX3-02: Fallback History Endpoint (ST-70) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/circuit-breaker/{boxId}/fallback-history
app.MapGet("/api/circuit-breaker/{boxId:guid}/fallback-history", async (
    Guid boxId, AppDbContext db, CancellationToken ct, int limit = 20) =>
{
    var logs = await db.FallbackAttemptLogs
        .Where(f => f.BoxId == boxId)
        .OrderByDescending(f => f.Timestamp)
        .Take(limit)
        .Select(f => new
        {
            f.Id, f.TaskId, Strategy = f.Strategy.ToString(),
            f.Success, f.DurationMs, f.FromModel, f.ToModel, f.Message, f.Timestamp
        })
        .ToListAsync(ct);
    return Results.Ok(logs);
});

// â”€â”€ BOX3-02: Rate Limit REST API (ST-73) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/rate-limit â€” status de todos os providers
app.MapGet("/api/rate-limit", (RateLimiterService rl) =>
{
    var limits = rl.GetAllLimits();
    var result = limits.Select(kv => new
    {
        provider           = kv.Key,
        maxRpm             = kv.Value,
        currentRpm         = rl.GetCurrentRpm(kv.Key),
        utilizationPercent = Math.Round(rl.GetUtilizationPercent(kv.Key), 1),
        tokensAvailable    = Math.Max(0, kv.Value - rl.GetCurrentRpm(kv.Key)),
    });
    return Results.Ok(result);
});

// GET /api/rate-limit/{provider} â€” detalhe de um provider
app.MapGet("/api/rate-limit/{provider}", (string provider, RateLimiterService rl) =>
    Results.Ok(new
    {
        provider,
        maxRpm             = rl.GetAllLimits().TryGetValue(provider, out var max) ? max : 0,
        currentRpm         = rl.GetCurrentRpm(provider),
        utilizationPercent = Math.Round(rl.GetUtilizationPercent(provider), 1),
    }));

// POST /api/rate-limit/{provider}/override â€” admin override temporÃ¡rio
app.MapPost("/api/rate-limit/{provider}/override", (string provider, RateLimitOverrideRequest req, RateLimiterService rl) =>
{
    rl.OverrideRpm(provider, req.NewRpm);
    return Results.Ok(new { provider, newRpm = req.NewRpm, message = "Override applied (runtime only)." });
});

// â”€â”€ BOX3-02: Token Budget REST API (ST-75 / ST-76) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/budget â€” todos os budgets
app.MapGet("/api/budget", async (ICostGuardService guard, CancellationToken ct) =>
    Results.Ok((await guard.GetAllBudgetsAsync(ct)).Select(b => b.ToDto())));

// GET /api/budget/{scope}/{scopeId}
app.MapGet("/api/budget/{scope}/{scopeId}", async (
    int scope, string scopeId, ICostGuardService guard, CancellationToken ct) =>
{
    var b = await guard.GetBudgetAsync((BudgetScope)scope, scopeId, ct);
    return b is null ? Results.NotFound() : Results.Ok(b.ToDto());
});

// PUT /api/budget/{scope}/{scopeId} â€” cria ou atualiza budget
app.MapPut("/api/budget/{scope}/{scopeId}", async (
    int scope, string scopeId, UpsertBudgetRequest req, ICostGuardService guard, CancellationToken ct) =>
{
    var b = await guard.UpsertBudgetAsync(
        (BudgetScope)scope, scopeId, req.BudgetTokens,
        req.AlertThresholdPercent, req.HardStopPercent, ct);
    return Results.Ok(b.ToDto());
});

// POST /api/budget/{scope}/{scopeId}/usage â€” registra uso de tokens
app.MapPost("/api/budget/{scope}/{scopeId}/usage", async (
    int scope, string scopeId, RecordUsageRequest req, ICostGuardService guard, CancellationToken ct) =>
{
    var result = await guard.RecordUsageAsync(
        (BudgetScope)scope, scopeId,
        req.TokensUsed, req.Model, req.Provider, ct: ct);
    return Results.Ok(result);
});

// POST /api/budget/{scope}/{scopeId}/kill â€” kill switch de emergÃªncia
app.MapPost("/api/budget/{scope}/{scopeId}/kill", async (
    int scope, string scopeId, ICostGuardService guard, CancellationToken ct) =>
{
    await guard.KillSwitchAsync((BudgetScope)scope, scopeId, ct);
    return Results.Ok(new { message = $"Kill switch activated for {(BudgetScope)scope}/{scopeId}" });
});

// BOX5: Prompt Cache (ST-91)

app.MapGet("/api/prompt-cache/stats", async (BriefappTodoList.Api.Services.PromptCache.IPromptCacheService cache, CancellationToken ct) =>
    Results.Ok(await cache.GetCacheStatsAsync(null, ct)));

app.MapGet("/api/prompt-cache/segments/all", async (AppDbContext db, CancellationToken ct) => 
{
    var segments = await db.PromptCacheEntries.OrderByDescending(x => x.LastUsedAt).Take(100).ToListAsync(ct);
    return Results.Ok(segments);
});

app.MapGet("/api/prompt-cache/{boxId:guid}/stats", async (Guid boxId, BriefappTodoList.Api.Services.PromptCache.IPromptCacheService cache, CancellationToken ct) =>
    Results.Ok(await cache.GetCacheStatsAsync(boxId, ct)));

app.MapPost("/api/prompt-cache/{boxId:guid}/warm", async (Guid boxId, BriefappTodoList.Api.Services.PromptCache.IPromptCacheService cache, CancellationToken ct) =>
{
    await cache.WarmCacheAsync(boxId, ct);
    return Results.Ok(new { message = "Cache warmup completed." });
});

app.MapPost("/api/prompt-cache/{boxId:guid}/invalidate", async (Guid boxId, BriefappTodoList.Api.Domain.PromptCache.PromptSegmentType? segmentType, BriefappTodoList.Api.Services.PromptCache.IPromptCacheService cache, CancellationToken ct) =>
{
    await cache.InvalidateCacheAsync(boxId, segmentType, ct);
    return Results.Ok(new { message = "Cache invalidated." });
});

app.MapGet("/api/prompt-cache/{boxId:guid}/segments", async (Guid boxId, BriefappTodoList.Api.Services.PromptCache.IPromptCacheService cache, CancellationToken ct) =>
{
    var segments = await cache.GetCacheableSegmentsAsync(boxId, ct);
    return Results.Ok(segments);
});

app.MapPut("/api/prompt-cache/{boxId:guid}/segments/{type}", async (Guid boxId, string type, BriefappTodoList.Api.Services.PromptCache.UpsertSegmentRequest req, BriefappTodoList.Api.Services.PromptCache.IPromptCacheService cache, CancellationToken ct) =>
{
    if (!Enum.TryParse<BriefappTodoList.Api.Domain.PromptCache.PromptSegmentType>(type, true, out var segmentType))
        return Results.BadRequest(new { error = "Invalid segment type." });

    var entry = await cache.UpsertSegmentAsync(boxId, segmentType, req.Content, ct);
    return Results.Ok(entry);
});

// â”€â”€ BOX1-02: Sandbox Monitor REST (ST-35) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/sandboxes â€” list all sandboxes with status
app.MapGet("/api/sandboxes", async (AppDbContext db, CancellationToken ct) =>
{
    var sandboxes = await db.Sandboxes.AsNoTracking()
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => new
        {
            s.Id,
            TaskId      = s.TaskId ?? "â€”",
            Image       = s.ImageName,
            Status      = s.Status.ToString(),
            s.StartedAt, s.StoppedAt, s.ContainerId,
            CpuLimit    = s.CpuCores,
            MemoryLimitMb = s.MemoryMb,
            s.CreatedAt,
        })
        .ToListAsync(ct);
    return Results.Ok(sandboxes);
});

// GET /api/sandboxes/{id}/metrics?limit=100 â€” metric snapshots
app.MapGet("/api/sandboxes/{id:guid}/metrics", async (
    Guid id, AppDbContext db, CancellationToken ct, int limit = 100) =>
{
    var rows = await db.SandboxMetricSnapshots
        .AsNoTracking()
        .Where(m => m.SandboxId == id)
        .OrderByDescending(m => m.CapturedAt)
        .Take(limit)
        .Select(m => new
        {
            m.CpuPercent, m.MemoryMb, m.MemoryPercent,
            m.NetworkRxBytes, m.NetworkTxBytes,
            m.DiskReadBytes, m.DiskWriteBytes,
            m.UptimeSeconds, m.CapturedAt,
        })
        .ToListAsync(ct);
    return Results.Ok(rows);
});

// GET /api/sandboxes/{id}/network-policy â€” active network policy
app.MapGet("/api/sandboxes/{id:guid}/network-policy", async (
    Guid id, SandboxNetworkPolicyService netPolicy, CancellationToken ct) =>
{
    var policy = await netPolicy.GetPolicyAsync(id, ct);
    return policy is null
        ? Results.NotFound(new { error = "No network policy configured." })
        : Results.Ok(policy);
});

// POST /api/sandboxes/{id}/metrics/collect â€” on-demand metric collection
app.MapPost("/api/sandboxes/{id:guid}/metrics/collect", async (
    Guid id, AppDbContext db, SandboxMetricsService metrics, CancellationToken ct) =>
{
    var sandbox = await db.Sandboxes.FindAsync([id], ct);
    if (sandbox is null || sandbox.ContainerId is null)
        return Results.NotFound(new { error = "Sandbox not found or not running." });
    var snap = await metrics.CollectAndPersistAsync(id, sandbox.ContainerId, ct);
    return snap is null ? Results.StatusCode(202) : Results.Ok(snap);
});

// â”€â”€ BOX4-02: Sessions REST (ST-81 / ST-83) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/sessions/{userId}/{channelType}/{boxId} â€” get or create session
app.MapGet("/api/sessions/{userId}/{channelType:int}/{boxId:guid}", async (
    string userId, int channelType, Guid boxId,
    SessionService sessions, CancellationToken ct) =>
{
    var session = await sessions.GetOrCreateAsync(userId, (ChannelType)channelType, boxId, ct);
    return Results.Ok(session);
});

// GET /api/sessions/{id}/history â€” conversation history
app.MapGet("/api/sessions/{id:guid}/history", async (
    Guid id, SessionService sessions, CancellationToken ct) =>
{
    var history = await sessions.GetConversationHistoryAsync(id, 50, ct);
    return Results.Ok(history);
});

// POST /api/sessions/{id}/handoff â€” request human handoff
app.MapPost("/api/sessions/{id:guid}/handoff", async (
    Guid id, SessionService sessions, CancellationToken ct) =>
{
    var session = await sessions.RequestHumanHandoffAsync(id, "Manually requested via API", ct);
    return session is null ? Results.NotFound() : Results.Ok(session);
});

// POST /api/sessions/{id}/return â€” return to agent
app.MapPost("/api/sessions/{id:guid}/return", async (
    Guid id, SessionService sessions, CancellationToken ct) =>
{
    var session = await sessions.ReturnToAgentAsync(id, ct);
    return session is null ? Results.NotFound() : Results.Ok(session);
});

// â”€â”€ BOX4-02: Channel Health (ST-84 / ST-86) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// GET /api/openclaw/health â€” all channel health statuses
app.MapGet("/api/openclaw/health", async (AppDbContext db, CancellationToken ct) =>
{
    var healths = await db.ChannelHealths.AsNoTracking()
        .OrderBy(h => h.ChannelType)
        .Select(h => new
        {
            Channel        = h.ChannelType.ToString(),
            h.Status,
            StatusLabel    = h.Status.ToString(),
            h.LastCheckAt, h.DownSince, h.FailoverTarget,
            h.UptimePercent, h.DeliveryRate, h.AvgLatencyMs,
            h.CheckCount, h.FailureCount,
        })
        .ToListAsync(ct);
    return Results.Ok(healths);
});

// GET /api/openclaw/retry-queue/stats â€” retry queue stats
app.MapGet("/api/openclaw/retry-queue/stats", (MessageRetryQueue retryQueue) =>
{
    var stats = retryQueue.GetStats();
    return Results.Ok(new
    {
        pending       = retryQueue.PendingCount,
        byChannel     = stats.GroupBy(s => s.Channel).ToDictionary(
            g => g.Key.ToString(), g => g.Count()),
        details       = stats.Take(20),
    });
});

// POST /api/openclaw/format â€” test formatter endpoint
app.MapPost("/api/openclaw/format", (
    string content, int channel,
    IEnumerable<IChannelFormatter> formatters) =>
{
    var formatter = formatters.FirstOrDefault(f => (int)f.Channel == channel);
    if (formatter is null) return Results.BadRequest(new { error = "Unknown channel." });
    var result = formatter.Format(content);
    return Results.Ok(result);
});

// ── Azure DevOps Sync endpoints ─────────────────────────────────────────────
app.MapPost("/api/azuredevops/sync", async (Guid? projectId, IServiceProvider sp, AppDbContext db, CancellationToken ct) =>
{
    // Try to get worker either directly or from hosted services
    var worker = sp.GetService<AzureDevOpsSyncWorker>() ?? sp.GetServices<IHostedService>().OfType<AzureDevOpsSyncWorker>().FirstOrDefault();
    if (worker is null)
        return Results.BadRequest(new { error = "Azure DevOps integration service is missing. Set AzureDevOps:Enabled=true in configuration." });

    if (projectId.HasValue)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId.Value, ct);
        if (project is null || !project.AdoEnabled)
            return Results.BadRequest(new { error = "Project not found or Azure DevOps not enabled for this project." });
    }

    // Force full sync when triggered manually via button
    var result = await worker.SyncAsync(forceAll: true, projectId: projectId, ct: ct);

    return Results.Ok(new
    {
        synced = result.Synced,
        failed = result.Failed,
        backlogsSynced = result.BacklogsSynced,
        backlogsFailed = result.BacklogsFailed,
        sprintsSynced = result.SprintsSynced,
        sprintsFailed = result.SprintsFailed,
        tasksSynced = result.TasksSynced,
        tasksFailed = result.TasksFailed,
        knowledgeSynced = result.KnowledgeSynced,
        knowledgeFailed = result.KnowledgeFailed,
        errorMessage = result.ErrorMessage,
        triggeredAt = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/azuredevops/status", (IServiceProvider sp, AppDbContext db) =>
{
    var worker = sp.GetService<AzureDevOpsSyncWorker>();
    var totalMappings = db.AzureDevOpsMappings.Count();
    return Results.Ok(new
    {
        enabled = worker is not null,
        lastSyncAt = worker?.LastSyncAt,
        lastSyncCount = worker?.LastSyncCount ?? 0,
        syncIntervalMinutes = worker?.SyncIntervalMinutes ?? 0,
        totalMappings
    });
});

app.MapPost("/api/azuredevops/test", async (
    [Microsoft.AspNetCore.Mvc.FromBody] BriefappTodoList.Api.Contracts.TestAdoRequest req, 
    AzureDevOpsSyncService service, 
    CancellationToken ct) =>
{
    if (service is null)
        return Results.BadRequest(new { error = "Azure DevOps integration is not enabled in backend." });

    if (string.IsNullOrWhiteSpace(req.Organization) || string.IsNullOrWhiteSpace(req.Pat))
    {
        return Results.BadRequest(new { error = "Organization or PAT is missing." });
    }

    var success = await service.TestConnectionAsync(req.Organization, req.Project ?? "", req.Pat, ct);
    
    if (!success) return Results.Ok(new { success = false });
    return Results.Ok(new { success });
});

// ── Azure DevOps Webhook Receiver ─────────────────────────────────────────────
app.MapPost("/api/azuredevops/webhook", async (
    HttpContext ctx,
    ILogger<Program> adoLogger,
    IServiceProvider sp,
    CancellationToken ct) =>
{
    // Read the raw body
    ctx.Request.EnableBuffering();
    using var ms = new System.IO.MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms, ct);
    var bodyBytes = ms.ToArray();

    if (bodyBytes.Length == 0)
        return Results.BadRequest(new { error = "Empty body" });

    try
    {
        var payload = System.Text.Json.JsonDocument.Parse(bodyBytes).RootElement;

        var eventType = payload.TryGetProperty("eventType", out var et) ? et.GetString() ?? "" : "";
        var publisherId = payload.TryGetProperty("publisherId", out var pi) ? pi.GetString() ?? "" : "";

        // Extract work item details
        int? workItemId = null;
        string? workItemTitle = null;
        string? workItemState = null;

        if (payload.TryGetProperty("resource", out var resource))
        {
            if (resource.TryGetProperty("id", out var wiId) && wiId.ValueKind == System.Text.Json.JsonValueKind.Number)
                workItemId = wiId.GetInt32();
            if (resource.TryGetProperty("fields", out var fields))
            {
                if (fields.TryGetProperty("System.Title", out var title))
                    workItemTitle = title.GetString();
                if (fields.TryGetProperty("System.State", out var state))
                    workItemState = state.GetString();
            }
            // For workitem.updated, details may be in revision
            if (resource.TryGetProperty("revision", out var revision))
            {
                if (revision.TryGetProperty("fields", out var revFields))
                {
                    if (revFields.TryGetProperty("System.Title", out var revTitle))
                        workItemTitle ??= revTitle.GetString();
                    if (revFields.TryGetProperty("System.State", out var revState))
                        workItemState ??= revState.GetString();
                }
                if (workItemId is null && revision.TryGetProperty("id", out var revId) && revId.ValueKind == System.Text.Json.JsonValueKind.Number)
                    workItemId = revId.GetInt32();
            }
        }

        adoLogger.LogInformation(
            "[AzureDevOps Webhook] Received {EventType} from {Publisher} — WorkItem #{WiId} \"{Title}\" State={State}",
            eventType, publisherId, workItemId, workItemTitle, workItemState);

        // Fan out to SSE subscribers if available
        var events = sp.GetService<MetricsEventService>();
        events?.Publish(new MetricsEvent($"ado.{eventType}", new
        {
            eventType,
            workItemId,
            workItemTitle,
            workItemState,
            receivedAt = DateTimeOffset.UtcNow
        }));

        // ── INBOUND SYNC: Process ADO changes back to Briefapp ──
        if (workItemId.HasValue && eventType.Contains("updated"))
        {
            var autoSync = sp.GetService<AdoAutoSyncService>();
            autoSync?.EnqueueInboundSync(workItemId.Value, workItemState, workItemTitle);
            adoLogger.LogInformation("[AzureDevOps Webhook] Enqueued inbound sync for ADO #{WiId}", workItemId);
        }

        return Results.Ok(new
        {
            received = true,
            eventType,
            workItemId,
            workItemTitle,
            workItemState,
            inboundSyncEnqueued = workItemId.HasValue && eventType.Contains("updated"),
            processedAt = DateTimeOffset.UtcNow
        });
    }
    catch (System.Text.Json.JsonException ex)
    {
        adoLogger.LogWarning(ex, "[AzureDevOps Webhook] Invalid JSON payload");
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }
});

app.Run();

static HumanEvaluationDto ToDto(HumanEvaluationEntity e) => new(
    e.Id, e.AgentRunId, e.ReviewerId, e.Score,
    e.AccuracyScore, e.RelevanceScore, e.CompletenessScore, e.SafetyScore,
    e.FeedbackText, e.RequiresEscalation, e.ReviewTimeSeconds, e.SubmittedAt);

static async Task<object> ArchiveProjectAsync(AppDbContext db, ProjectEntity project, CancellationToken ct)
{
    if (project.Status == ProjectStatus.Archived)
    {
        return new { archived = true, alreadyArchived = true, projectId = project.Id };
    }

    project.Status = ProjectStatus.Archived;
    project.ArchivedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);

    return new { archived = true, alreadyArchived = false, projectId = project.Id, archivedAt = project.ArchivedAt };
}

static BoxUserRole ParseBoxUserRole(string? role) => role?.Trim().ToLowerInvariant() switch
{
    "owner" => BoxUserRole.Owner,
    "admin" => BoxUserRole.Admin,
    "editor" => BoxUserRole.Editor,
    _ => BoxUserRole.Viewer,
};

public class AgentMonitorService : BackgroundService
{
    private readonly IServiceProvider _sp;
    public AgentMonitorService(IServiceProvider sp) { _sp = sp; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var threshold = DateTimeOffset.UtcNow.AddMinutes(-5);
            var inactiveRuns = await db.AgentRunLogs
                .Where(r => r.Status == "running" && r.LastActivityAt < threshold)
                .ToListAsync(stoppingToken);
            foreach (var run in inactiveRuns)
            {
                run.Status = "inactive";
                run.FinishedAt = DateTimeOffset.UtcNow;
            }
            if (inactiveRuns.Any()) await db.SaveChangesAsync(stoppingToken);
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public partial class Program;

