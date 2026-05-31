namespace BriefappTodoList.Api.Domain;


/// <summary>
/// Representa um nó no grafo de rastreabilidade.
/// Pode ser uma task, commit, arquivo, regra de negócio, agente, etc.
/// </summary>
public class GraphNodeEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Projeto ao qual este nó pertence.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Tipo do nó: task | commit | file | business_rule |
    /// acceptance_criteria | sprint | backlog | agent
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// ID externo estável que identifica unicamente o nó dentro do tipo.
    /// Ex: UUID de work item, hash de commit, caminho de arquivo.
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Label legível exibida na visualização do grafo.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Propriedades adicionais em JSON (status, branch, author, etc.).</summary>
    public string PropertiesJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ProjectEntity? Project { get; set; }
    public ICollection<GraphEdgeEntity> OutgoingEdges { get; set; } = new List<GraphEdgeEntity>();
    public ICollection<GraphEdgeEntity> IncomingEdges { get; set; } = new List<GraphEdgeEntity>();
}
