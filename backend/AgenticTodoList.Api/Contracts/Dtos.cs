using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Contracts;

public record CreateProjectRequest(
    string Name,
    string Description,
    string? GitHubUrl = null,
    string? LocalPath = null,
    string? TechStack = null,
    string? MainBranch = null);

public record UpdateProjectConfigRequest(
    string? GitHubUrl,
    string? LocalPath,
    string? TechStack,
    string? MainBranch,
    bool? AdoEnabled = null,
    string? AdoOrganization = null,
    string? AdoProject = null,
    string? AdoPat = null);
public record TestAdoRequest(string Organization, string Project, string Pat);
public record AddBacklogItemRequest(string Title, string Description, int StoryPoints, int Priority, string[]? CommitIds = null);
public record CreateSprintRequest(string Name, string Goal, DateOnly StartDate, DateOnly EndDate, Guid[] BacklogItemIds, string[]? CommitIds = null);
public record UpdateWorkItemStatusRequest(
    WorkItemStatus Status,
    string Assignee,
    Guid? ResponsavelId = null,
    int TokensUsed = 0,
    string AgentName = "",
    string ModelUsed = "",
    string IdeUsed = "",
    string Feedback = "",
    string MetadataJson = "",
    string Branch = "",
    string[]? CommitIds = null);
public record AddWorkItemRequest(string Title, string Description, string Assignee = "", string Branch = "", string Tags = "");
public record AddSubTaskRequest(string Title, string Description, string Assignee = "", string Branch = "", string Tags = "");
public record UpdateBacklogItemContextRequest(string? Tags, string? WikiRefs, string? Constraints, string[]? CommitIds = null);
public record UpdateSprintCommitIdsRequest(string[] CommitIds);
public record AddReviewRequest(string Type, string Summary, string Notes);
public record AddWikiPageRequest(string Title, string ContentMarkdown, string Tags, string Category = "General");
public record AddCheckpointRequest(string Name, string ContextSnapshot, string Decisions, string Risks, string NextActions, string Category = "General");
public record AddDocumentationPageRequest(string Title, string ContentMarkdown, string Category, string Tags);
public record AddAgentRunLogRequest(
    string AgentName,
    string EntryPoint,
    string InputSummary,
    string OutputSummary,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    // DevLake metrics fields (SP-03 BL-05)
    string ModelName = "",
    int TokensInput = 0,
    int TokensOutput = 0,
    long LatencyMs = 0,
    decimal CostUsd = 0m,
    bool Success = true,
    string ErrorMessage = "",
    string Environment = "production");

// Human Evaluation requests/responses (SP-03 BL-06)
public record SubmitHumanEvaluationRequest(
    string ReviewerId,
    float AccuracyScore,    // 0.0-1.0
    float RelevanceScore,   // 0.0-1.0
    float CompletenessScore,// 0.0-1.0
    float SafetyScore,      // 0.0-1.0
    string FeedbackText = "",
    bool RequiresEscalation = false,
    long ReviewTimeSeconds = 0);

public record HumanEvaluationDto(
    Guid Id,
    Guid AgentRunId,
    string ReviewerId,
    float Score,
    float AccuracyScore,
    float RelevanceScore,
    float CompletenessScore,
    float SafetyScore,
    string FeedbackText,
    bool RequiresEscalation,
    long ReviewTimeSeconds,
    DateTimeOffset SubmittedAt);

public record DashboardDto(
    Guid ProjectId,
    string ProjectName,
    int BacklogTotal,
    int BacklogDone,
    int ActiveSprints,
    int WorkItemsTodo,
    int WorkItemsInProgress,
    int WorkItemsReview,
    int WorkItemsDone,
    int KnowledgeCheckpoints,
    int WikiPages,
    int AgentRuns);

// Box Users (v3)
public record AddBoxUserRequest(string Email, string Role = "viewer", string Groups = "");
public record UpdateBoxUserRequest(string? Role, string? Groups);

// Memory-Box (v3)
public record UpsertMemoryRequest(string Key, string Value, string Tags = "");

// Box Logs (v3)
public record CreateLogRequest(string Level = "info", string Source = "", string Message = "", string Details = "");

// Box API Keys (v3)
public record CreateApiKeyRequest(string Name, string Scopes = "read");


// Allow-List (v3)
public record UpsertAllowListRequest(string AppName, string CallbackUrl = "", string Scopes = "read");

// Usage Module (v3)
public record BoxUsageSummaryDto(
    int TotalRuns,
    int TotalTokensInput,
    int TotalTokensOutput,
    decimal TotalCostUsd,
    double SuccessRatePct,
    Dictionary<string, int> RunsByModel
);

// Global Search (v3)
public record SearchResultItemDto(
    string Id,
    string Type,
    string Title,
    string? Description,
    string? Status,
    Guid? ProjectId,
    string? ProjectName,
    DateTimeOffset CreatedAt,
    float? Score = null);

public record SearchResponseDto(
    string Query,
    int TotalResults,
    List<SearchResultItemDto> Results);

