namespace BriefappTodoList.Api.Domain;


/// <summary>
/// Representa uma aresta direcional no grafo de rastreabilidade.
/// Conecta dois nós com um tipo semântico de relação.
/// </summary>
public class GraphEdgeEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }

    /// <summary>
    /// Tipo da aresta: implements | satisfies | produced | modifies |
    /// belongs_to | executed_by | depends_on | references | related_to
    /// </summary>
    public string EdgeType { get; set; } = string.Empty;

    /// <summary>Peso da aresta (frequência de co-ocorrência ou relevância).</summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>Metadados adicionais da aresta em JSON.</summary>
    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ProjectEntity? Project { get; set; }
    public GraphNodeEntity? SourceNode { get; set; }
    public GraphNodeEntity? TargetNode { get; set; }
}
