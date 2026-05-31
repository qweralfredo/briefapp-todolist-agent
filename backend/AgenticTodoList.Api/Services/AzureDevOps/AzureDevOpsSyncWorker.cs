using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Services.AzureDevOps;

/// <summary>
/// Periodic background worker that syncs Briefapp WorkItems to Azure DevOps.
/// Follows the same pattern as DevLakeSyncWorker: resilient, non-blocking, fire-and-forget.
/// Configurable via AzureDevOps:SyncIntervalMinutes (default 5).
/// </summary>
public sealed class AzureDevOpsSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AzureDevOpsSyncWorker> _logger;
    private readonly IConfiguration _config;
    private readonly TimeSpan _interval;

    public DateTimeOffset? LastSyncAt { get; private set; }
    public int LastSyncCount { get; private set; }
    public int SyncIntervalMinutes { get; }

    public AzureDevOpsSyncWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<AzureDevOpsSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
        SyncIntervalMinutes = config.GetValue<int>("AzureDevOps:SyncIntervalMinutes", 5);
        _interval = TimeSpan.FromMinutes(SyncIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup delay to allow the application to warm up
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ContinueWith(_ => { });

        _logger.LogInformation("[AzureDevOpsSyncWorker] Started — sync interval: {Interval}min", SyncIntervalMinutes);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAsync(false, null, stoppingToken);
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Manually trigger a full sync cycle (called from /api/azuredevops/sync).</summary>
    public async Task<SyncResult> SyncAsync(bool forceAll = false, Guid? projectId = null, CancellationToken ct = default)
    {
        var result = new SyncResult();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<AzureDevOpsSyncService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var projects = await db.Projects
                .Where(p => p.AdoEnabled && p.Status == ProjectStatus.Active)
                .Where(p => !projectId.HasValue || p.Id == projectId.Value)
                .ToListAsync(ct);

            if (projects.Count == 0)
            {
                _logger.LogDebug("[AzureDevOpsSyncWorker] No projects have Azure DevOps integration enabled — skipping");
                LastSyncAt = DateTimeOffset.UtcNow;
                return result;
            }

            var since = forceAll ? DateTimeOffset.MinValue : (LastSyncAt ?? DateTimeOffset.UtcNow.AddHours(-1));
            int totalChecked = 0;

            foreach (var project in projects)
            {
                // Always ensure team Area path and backlogIteration are configured before syncing
                await syncService.EnsureTeamSettingsAsync(project, ct);

                if (forceAll)
                {
                    await syncService.EnsureProjectInfrastructureAsync(project, ct);
                    await syncService.SyncProjectDescriptionAsync(project, ct);
                }

                // 1. Sync Backlogs
                var backlogs = await db.BacklogItems
                    .Where(b => b.ProjectId == project.Id && (forceAll || b.CreatedAt >= since))
                    .ToListAsync(ct);

                totalChecked += backlogs.Count;
                var backlogAdoMap = new Dictionary<Guid, int>();

                foreach (var bl in backlogs)
                {
                    var map = await syncService.SyncGenericItemAsync(project, bl.Id, "Backlog", bl.Title, bl.Description, bl.Tags, "Issue", ct);
                    if (map is not null) { result.Synced++; result.BacklogsSynced++; backlogAdoMap[bl.Id] = map.AzureDevOpsWorkItemId; }
                    else { result.Failed++; result.BacklogsFailed++; }
                }

                // Always need full ADO IDs for work items linking
                if (!forceAll)
                {
                    var existingMaps = await db.AzureDevOpsMappings
                        .Where(m => m.BriefappBacklogItemId != null && m.BriefappBacklogItem!.ProjectId == project.Id)
                        .ToListAsync(ct);
                    foreach (var m in existingMaps) backlogAdoMap[m.BriefappBacklogItemId!.Value] = m.AzureDevOpsWorkItemId;
                }

                // 2. Sync Knowledge (Wikis, Docs, Checkpoints)
                var wikis = await db.WikiPages.Where(w => w.ProjectId == project.Id && (forceAll || w.CreatedAt >= since || w.UpdatedAt >= since)).ToListAsync(ct);
                totalChecked += wikis.Count;
                foreach (var w in wikis) { if (await syncService.SyncGenericItemAsync(project, w.Id, "Wiki", $"[Wiki] {w.Title}", w.ContentMarkdown, $"Wiki;{w.Category};{w.Tags}", "Issue", ct) is not null) { result.Synced++; result.KnowledgeSynced++; } else { result.Failed++; result.KnowledgeFailed++; } }

                var docs = await db.DocumentationPages.Where(d => d.ProjectId == project.Id && (forceAll || d.CreatedAt >= since || d.UpdatedAt >= since)).ToListAsync(ct);
                totalChecked += docs.Count;
                foreach (var d in docs) { if (await syncService.SyncGenericItemAsync(project, d.Id, "Doc", $"[Doc] {d.Title}", d.ContentMarkdown, $"Documentation;{d.Category};{d.Tags}", "Issue", ct) is not null) { result.Synced++; result.KnowledgeSynced++; } else { result.Failed++; result.KnowledgeFailed++; } }

                var checks = await db.KnowledgeCheckpoints.Where(c => c.ProjectId == project.Id && (forceAll || c.CreatedAt >= since)).ToListAsync(ct);
                totalChecked += checks.Count;
                foreach (var c in checks)
                {
                    var desc = $"Decisions: {c.Decisions}\n\nRisks: {c.Risks}\n\nNext Actions: {c.NextActions}";
                    if (await syncService.SyncGenericItemAsync(project, c.Id, "Checkpoint", $"[Checkpoint] {c.Name}", desc, "Checkpoint", "Issue", ct) is not null) { result.Synced++; result.KnowledgeSynced++; } else { result.Failed++; result.KnowledgeFailed++; }
                }

                // 3. Sync Sprints
                var sprints = await db.Sprints.Where(s => s.ProjectId == project.Id && (forceAll || s.CreatedAt >= since)).ToListAsync(ct);
                totalChecked += sprints.Count;
                var sprintAdoMap = new Dictionary<Guid, int>();
                var sprintNameMap = new Dictionary<Guid, string>();
                
                foreach (var sp in sprints)
                {
                    await syncService.EnsureIterationAsync(project, sp, ct);
                    sprintNameMap[sp.Id] = sp.Name;

                    var map = await syncService.SyncGenericItemAsync(project, sp.Id, "Sprint", $"[Sprint] {sp.Name}", $"Goal: {sp.Goal}\nStart: {sp.StartDate:yyyy-MM-dd}\nEnd: {sp.EndDate:yyyy-MM-dd}", "Sprint", "Epic", ct);
                    if (map is not null) { result.Synced++; result.SprintsSynced++; sprintAdoMap[sp.Id] = map.AzureDevOpsWorkItemId; }
                    else { result.Failed++; result.SprintsFailed++; }
                }

                if (!forceAll)
                {
                    // To ensure we have sprintNameMap populated for tasks even if we didn't sync the sprint in this run
                    var allSprints = await db.Sprints.Where(s => s.ProjectId == project.Id).ToListAsync(ct);
                    foreach (var s in allSprints) sprintNameMap[s.Id] = s.Name;

                    var existingMaps = await db.AzureDevOpsMappings.Where(m => m.BriefappSprintId != null && m.BriefappSprint!.ProjectId == project.Id).ToListAsync(ct);
                    foreach (var m in existingMaps) sprintAdoMap[m.BriefappSprintId!.Value] = m.AzureDevOpsWorkItemId;
                }

                // 4. Sync WorkItems (Tasks & SubTasks)
                var workItems = await db.WorkItems
                    .Where(w => w.BacklogItem.ProjectId == project.Id && (forceAll || w.CreatedAt >= since || (w.UpdatedAt != null && w.UpdatedAt >= since)))
                    .OrderBy(w => w.ParentWorkItemId == null ? 0 : 1)
                    .ThenBy(w => w.CreatedAt)
                    .ToListAsync(ct);

                totalChecked += workItems.Count;
                var workItemAdoMap = new Dictionary<Guid, int>();

                if (!forceAll)
                {
                    var existingWIMaps = await db.AzureDevOpsMappings.Where(m => m.BriefappWorkItemId != null && m.BriefappWorkItem!.Project!.Id == project.Id).ToListAsync(ct);
                    foreach (var m in existingWIMaps) workItemAdoMap[m.BriefappWorkItemId!.Value] = m.AzureDevOpsWorkItemId;
                }

                foreach (var workItem in workItems)
                {
                    int? parentId = null;
                    bool parentIsTask = false;
                    if (workItem.ParentWorkItemId.HasValue && workItemAdoMap.TryGetValue(workItem.ParentWorkItemId.Value, out var parentAdo))
                    {
                        parentId = parentAdo; // Subtask -> Task
                        parentIsTask = true;
                    }
                    else if (backlogAdoMap.TryGetValue(workItem.BacklogItemId, out var adoId))
                    {
                        parentId = adoId; // Task -> Backlog
                        parentIsTask = false;
                    }

                    int? sprintId = sprintAdoMap.TryGetValue(workItem.SprintId, out var sId) ? sId : null;
                    string? sprintName = sprintNameMap.TryGetValue(workItem.SprintId, out var sName) ? sName : null;

                    var mapping = await syncService.SyncWorkItemAsync(project, workItem, parentId, parentIsTask, sprintId, sprintName, ct);
                    if (mapping is not null)
                    {
                        result.Synced++;
                        result.TasksSynced++;
                        workItemAdoMap[workItem.Id] = mapping.AzureDevOpsWorkItemId;

                        // 5. Sync commit links for work items that have commits
                        if (workItem.CommitIds.Count > 0)
                        {
                            await syncService.SyncCommitLinksAsync(project, workItem, mapping.AzureDevOpsWorkItemId, ct);
                        }
                    }
                    else
                    {
                        result.Failed++;
                        result.TasksFailed++;
                    }
                }

                // 6. Sync Knowledge to ADO Wiki (only on forceAll)
                if (forceAll)
                {
                    var allWikis = await db.WikiPages.Where(w => w.ProjectId == project.Id).ToListAsync(ct);
                    var allDocs = await db.DocumentationPages.Where(d => d.ProjectId == project.Id).ToListAsync(ct);
                    var allCheckpoints = await db.KnowledgeCheckpoints.Where(c => c.ProjectId == project.Id).ToListAsync(ct);
                    await syncService.SyncKnowledgeToWikiAsync(project, allWikis, allDocs, allCheckpoints, ct);
                }

                // 7. Ensure webhook subscriptions are registered (only on forceAll)
                if (forceAll)
                {
                    var webhookBaseUrl = _config["AzureDevOps:WebhookBaseUrl"] ?? "http://76.13.238.113";
                    await syncService.EnsureWebhookSubscriptionsAsync(project, webhookBaseUrl, ct);
                }

                // 8. Ensure standard dashboards are created (only on forceAll)
                if (forceAll)
                {
                    await syncService.EnsureDashboardsAsync(project, ct);
                }
            }

            LastSyncAt = DateTimeOffset.UtcNow;
            LastSyncCount = result.Synced;

            _logger.LogInformation(
                "[AzureDevOpsSyncWorker] Sync complete: {Synced} synced, {Failed} failed, {Total} total work items checked",
                result.Synced, result.Failed, totalChecked);

            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AzureDevOpsSyncWorker] Sync cycle failed — will retry on next tick");
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public record SyncResult
    {
        public int Synced { get; set; }
        public int Failed { get; set; }
        public int BacklogsSynced { get; set; }
        public int BacklogsFailed { get; set; }
        public int SprintsSynced { get; set; }
        public int SprintsFailed { get; set; }
        public int TasksSynced { get; set; }
        public int TasksFailed { get; set; }
        public int KnowledgeSynced { get; set; }
        public int KnowledgeFailed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
