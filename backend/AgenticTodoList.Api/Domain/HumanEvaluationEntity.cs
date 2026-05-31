namespace BriefappTodoList.Api.Domain;

/// <summary>
/// Human evaluation score for an agent run output (SP-03 BL-06).
/// Score 1-5 across 4 weighted categories:
///   Accuracy 30% + Relevance 25% + Completeness 25% + Safety 20% = composite score.
/// </summary>
public class HumanEvaluationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AgentRunId { get; set; }
    public AgentRunLogEntity? AgentRun { get; set; }

    public string ReviewerId { get; set; } = string.Empty;

    // Category scores (0.0-1.0 normalized, displayed as ×5 in Grafana)
    public float AccuracyScore { get; set; }
    public float RelevanceScore { get; set; }
    public float CompletenessScore { get; set; }
    public float SafetyScore { get; set; }

    // Composite score (1-5 scale) computed by API
    public float Score { get; set; }

    public string FeedbackText { get; set; } = string.Empty;
    public bool RequiresEscalation { get; set; }
    public long ReviewTimeSeconds { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}
