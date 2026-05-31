namespace BriefappTodoList.Api.Domain;

public class AgentRunLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }

    public string AgentName { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = string.Empty;
    public string InputSummary { get; set; } = string.Empty;
    public string OutputSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }

    // DevLake metrics fields (SP-03 BL-05)
    public string ModelName { get; set; } = string.Empty;
    public int TokensInput { get; set; }
    public int TokensOutput { get; set; }
    public long LatencyMs { get; set; }
    public decimal CostUsd { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
    public string Environment { get; set; } = "production";

    // Navigation: human evaluations for this run
    public ICollection<HumanEvaluationEntity> HumanEvaluations { get; set; } = [];
}

