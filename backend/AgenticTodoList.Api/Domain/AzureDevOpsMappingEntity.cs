namespace BriefappTodoList.Api.Domain;

/// <summary>
/// Tracks the bidirectional mapping between Briefapp WorkItem IDs and Azure DevOps Work Item IDs.
/// Used by AzureDevOpsSyncService to determine whether to create or update a work item.
/// </summary>
public class AzureDevOpsMappingEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Briefapp WorkItem ID (FK to WorkItems table).</summary>
    public Guid? BriefappWorkItemId { get; set; }
    public WorkItemEntity? BriefappWorkItem { get; set; }

    /// <summary>Briefapp BacklogItem ID.</summary>
    public Guid? BriefappBacklogItemId { get; set; }
    public BacklogItemEntity? BriefappBacklogItem { get; set; }

    /// <summary>Briefapp Wiki ID.</summary>
    public Guid? BriefappWikiId { get; set; }
    public WikiPageEntity? BriefappWiki { get; set; }

    /// <summary>Briefapp Documentation ID.</summary>
    public Guid? BriefappDocumentationId { get; set; }
    public DocumentationPageEntity? BriefappDocumentation { get; set; }

    /// <summary>Briefapp Checkpoint ID.</summary>
    public Guid? BriefappCheckpointId { get; set; }
    public KnowledgeCheckpointEntity? BriefappCheckpoint { get; set; }

    /// <summary>Briefapp Sprint ID.</summary>
    public Guid? BriefappSprintId { get; set; }
    public SprintEntity? BriefappSprint { get; set; }

    /// <summary>Azure DevOps Work Item ID (integer returned by ADO API).</summary>
    public int AzureDevOpsWorkItemId { get; set; }

    /// <summary>Full URL to the work item in Azure DevOps (for quick reference).</summary>
    public string AzureDevOpsUrl { get; set; } = string.Empty;

    /// <summary>Last revision number synced from Azure DevOps.</summary>
    public int LastSyncedRev { get; set; }

    /// <summary>Last Briefapp status synced to Azure DevOps.</summary>
    public WorkItemStatus LastSyncedStatus { get; set; }

    /// <summary>Timestamp of the last successful synchronization.</summary>
    public DateTimeOffset LastSyncAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
