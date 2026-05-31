using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Services.AzureDevOps;

/// <summary>
/// Background service that automatically syncs individual Briefapp work items to Azure DevOps
/// when data changes. Uses a Channel-based queue for async, non-blocking sync.
/// </summary>
public class AdoAutoSyncService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<AdoAutoSyncService> _logger;
    private readonly Channel<SyncRequest> _channel = Channel.CreateBounded<SyncRequest>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });

    public AdoAutoSyncService(IServiceProvider sp, ILogger<AdoAutoSyncService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    /// <summary>
    /// Enqueue a work item for ADO sync. Fire-and-forget from API endpoints.
    /// </summary>
    public void EnqueueWorkItemSync(Guid workItemId) =>
        _channel.Writer.TryWrite(new SyncRequest(SyncRequestType.WorkItem, workItemId));

    /// <summary>
    /// Enqueue a full project sync (e.g., after sprint creation).
    /// </summary>
    public void EnqueueProjectSync(Guid projectId) =>
        _channel.Writer.TryWrite(new SyncRequest(SyncRequestType.Project, projectId));

    /// <summary>
    /// Process an inbound ADO webhook event — sync ADO changes back to Briefapp.
    /// </summary>
    public void EnqueueInboundSync(int adoWorkItemId, string? state, string? title) =>
        _channel.Writer.TryWrite(new SyncRequest(SyncRequestType.InboundFromAdo, Guid.Empty)
        {
            AdoWorkItemId = adoWorkItemId,
            AdoState = state,
            AdoTitle = title
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AdoAutoSync] Background sync service started");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var syncService = scope.ServiceProvider.GetRequiredService<AzureDevOpsSyncService>();

                switch (request.Type)
                {
                    case SyncRequestType.WorkItem:
                        await SyncSingleWorkItemAsync(db, syncService, request.EntityId, stoppingToken);
                        break;
                    case SyncRequestType.Project:
                        await SyncProjectAsync(db, scope.ServiceProvider, request.EntityId, stoppingToken);
                        break;
                    case SyncRequestType.InboundFromAdo:
                        await ProcessInboundAdoEventAsync(db, request, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AdoAutoSync] Error processing sync request {Type} {Id}",
                    request.Type, request.EntityId);
            }
        }
    }

    private async Task SyncSingleWorkItemAsync(AppDbContext db, AzureDevOpsSyncService syncService, Guid workItemId, CancellationToken ct)
    {
        var workItem = await db.WorkItems
            .Include(w => w.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == workItemId, ct);
        if (workItem is null) return;

        var sprint = await db.Sprints
            .FirstOrDefaultAsync(s => s.Id == workItem.SprintId, ct);
        if (sprint is null) return;

        var project = await db.Projects
            .FirstOrDefaultAsync(p => p.Id == sprint.ProjectId, ct);
        if (project is null || !project.AdoEnabled) return;

        // Get parent mapping if this is a sub-task, otherwise get backlog mapping
        int? parentAdoId = null;
        bool parentIsTask = false;
        if (workItem.ParentWorkItemId.HasValue)
        {
            var parentMapping = await db.AzureDevOpsMappings
                .FirstOrDefaultAsync(m => m.BriefappWorkItemId == workItem.ParentWorkItemId.Value, ct);
            parentAdoId = parentMapping?.AzureDevOpsWorkItemId;
            parentIsTask = true;
        }
        else
        {
            var backlogMapping = await db.AzureDevOpsMappings
                .FirstOrDefaultAsync(m => m.BriefappBacklogItemId == workItem.BacklogItemId, ct);
            parentAdoId = backlogMapping?.AzureDevOpsWorkItemId;
            parentIsTask = false;
        }

        int? sprintAdoId = null;
        var sprintMapping = await db.AzureDevOpsMappings
            .FirstOrDefaultAsync(m => m.BriefappSprintId == workItem.SprintId, ct);
        sprintAdoId = sprintMapping?.AzureDevOpsWorkItemId;

        var mapping = await syncService.SyncWorkItemAsync(project, workItem, parentAdoId, parentIsTask, sprintAdoId, sprint.Name, ct);
        if (mapping != null)
        {
            _logger.LogDebug("[AdoAutoSync] Synced WorkItem {Id} → ADO #{AdoId}", workItemId, mapping.AzureDevOpsWorkItemId);
        }
    }

    private async Task SyncProjectAsync(AppDbContext db, IServiceProvider sp, Guid projectId, CancellationToken ct)
    {
        var worker = sp.GetService<AzureDevOpsSyncWorker>()
            ?? sp.GetServices<IHostedService>().OfType<AzureDevOpsSyncWorker>().FirstOrDefault();
        if (worker is null) return;

        await worker.SyncAsync(forceAll: true, projectId: projectId, ct: ct);
        _logger.LogDebug("[AdoAutoSync] Full project sync triggered for {ProjectId}", projectId);
    }

    /// <summary>
    /// Processes inbound ADO webhook events — updates Briefapp work items based on ADO state changes.
    /// </summary>
    private async Task ProcessInboundAdoEventAsync(AppDbContext db, SyncRequest request, CancellationToken ct)
    {
        if (request.AdoWorkItemId <= 0) return;

        var mapping = await db.AzureDevOpsMappings
            .FirstOrDefaultAsync(m => m.AzureDevOpsWorkItemId == request.AdoWorkItemId, ct);
        if (mapping is null)
        {
            _logger.LogDebug("[AdoAutoSync] No Briefapp mapping found for ADO WorkItem #{AdoId}", request.AdoWorkItemId);
            return;
        }

        var workItem = await db.WorkItems
            .FirstOrDefaultAsync(w => w.Id == mapping.BriefappWorkItemId, ct);
        if (workItem is null) return;

        var updated = false;

        // Map ADO state back to Briefapp status
        if (!string.IsNullOrWhiteSpace(request.AdoState))
        {
            var newStatus = MapAdoStateToBriefapp(request.AdoState);
            if (newStatus != workItem.Status)
            {
                _logger.LogInformation("[AdoAutoSync] ADO #{AdoId} state change: {OldState} → {NewState} (Briefapp {BriefappId})",
                    request.AdoWorkItemId, workItem.Status, newStatus, workItem.Id);
                workItem.Status = newStatus;
                workItem.UpdatedAt = DateTimeOffset.UtcNow;
                updated = true;
            }
        }

        // Update title if changed from ADO
        if (!string.IsNullOrWhiteSpace(request.AdoTitle) && request.AdoTitle != workItem.Title)
        {
            workItem.Title = request.AdoTitle;
            workItem.UpdatedAt = DateTimeOffset.UtcNow;
            updated = true;
        }

        if (updated)
        {
            mapping.LastSyncedStatus = workItem.Status;
            mapping.LastSyncAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Maps Azure DevOps work item state names back to Briefapp WorkItemStatus.
    /// Supports standard Agile/Scrum states plus custom board columns.
    /// </summary>
    public static WorkItemStatus MapAdoStateToBriefapp(string adoState) => adoState?.Trim() switch
    {
        "To Do"             => WorkItemStatus.Todo,
        "New"               => WorkItemStatus.Todo,
        "Doing"             => WorkItemStatus.InProgress,
        "Active"            => WorkItemStatus.InProgress,
        "In Progress"       => WorkItemStatus.InProgress,
        "E2E"               => WorkItemStatus.Review,
        "Review"            => WorkItemStatus.Review,
        "Homologação"       => WorkItemStatus.Review,
        "Homologacao"       => WorkItemStatus.Review,
        "Human in the Loop" => WorkItemStatus.Blocked,
        "Bloqueadas"        => WorkItemStatus.Blocked,
        "Blocked"           => WorkItemStatus.Blocked,
        "Done"              => WorkItemStatus.Done,
        "Closed"            => WorkItemStatus.Done,
        "Resolved"          => WorkItemStatus.Done,
        "Removed"           => WorkItemStatus.Done,
        _                   => WorkItemStatus.Todo
    };

    private record SyncRequest(SyncRequestType Type, Guid EntityId)
    {
        public int AdoWorkItemId { get; init; }
        public string? AdoState { get; init; }
        public string? AdoTitle { get; init; }
    }

    private enum SyncRequestType
    {
        WorkItem,
        Project,
        InboundFromAdo
    }
}
