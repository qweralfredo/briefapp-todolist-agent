namespace BriefappTodoList.Api.Contracts;

public class AgentRunWebhookRequest
{
    public string RunId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string OutputSummary { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int TokensInput { get; set; }
    public int TokensOutput { get; set; }
}
