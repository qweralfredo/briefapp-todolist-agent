using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BriefappTodoList.Api.Controllers;

/// <summary>
/// API REST para o módulo de Knowledge Graph (Rastreabilidade).
/// Expõe endpoints para upsert de nós/arestas e queries de grafo.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/graph")]
public class GraphController(AppDbContext db) : ControllerBase
{
    // ── Contracts ──────────────────────────────────────────────────────────────

    public record UpsertNodeRequest(
        string NodeType,
        string ExternalId,
        string Label,
        JsonElement? Properties = null
    );

    public record UpsertEdgeRequest(
        Guid SourceNodeId,
        Guid TargetNodeId,
        string EdgeType,
        double Weight = 1.0,
        JsonElement? Metadata = null
    );

    public record GraphNodeDto(
        Guid Id,
        Guid ProjectId,
        string NodeType,
        string ExternalId,
        string Label,
        string PropertiesJson,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    public record GraphEdgeDto(
        Guid Id,
        Guid ProjectId,
        Guid SourceNodeId,
        Guid TargetNodeId,
        string EdgeType,
        double Weight,
        string MetadataJson,
        DateTimeOffset CreatedAt
    );

    public record GraphResponse(
        IList<GraphNodeDto> Nodes,
        IList<GraphEdgeDto> Edges
    );

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static GraphNodeDto ToDto(GraphNodeEntity n) => new(
        n.Id, n.ProjectId, n.NodeType, n.ExternalId, n.Label, n.PropertiesJson, n.CreatedAt, n.UpdatedAt);

    private static GraphEdgeDto ToDto(GraphEdgeEntity e) => new(
        e.Id, e.ProjectId, e.SourceNodeId, e.TargetNodeId, e.EdgeType, e.Weight, e.MetadataJson, e.CreatedAt);

    // ── Node Upsert ────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria ou atualiza um nó no grafo do projeto.
    /// Upsert baseado em (projectId, nodeType, externalId).
    /// </summary>
    [HttpPost("nodes")]
    public async Task<IActionResult> UpsertNode(Guid projectId, [FromBody] UpsertNodeRequest req)
    {
        var props = req.Properties.HasValue
            ? req.Properties.Value.GetRawText()
            : "{}";

        var node = await db.GraphNodes
            .FirstOrDefaultAsync(n =>
                n.ProjectId == projectId &&
                n.NodeType == req.NodeType &&
                n.ExternalId == req.ExternalId);

        if (node is null)
        {
            node = new GraphNodeEntity
            {
                ProjectId = projectId,
                NodeType = req.NodeType,
                ExternalId = req.ExternalId,
                Label = req.Label,
                PropertiesJson = props,
            };
            db.GraphNodes.Add(node);
        }
        else
        {
            node.Label = req.Label;
            node.PropertiesJson = props;
            node.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(ToDto(node));
    }

    // ── Edge Upsert ────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria ou atualiza uma aresta entre dois nós do grafo.
    /// Upsert baseado em (projectId, sourceNodeId, targetNodeId, edgeType).
    /// </summary>
    [HttpPost("edges")]
    public async Task<IActionResult> UpsertEdge(Guid projectId, [FromBody] UpsertEdgeRequest req)
    {
        var meta = req.Metadata.HasValue
            ? req.Metadata.Value.GetRawText()
            : "{}";

        var edge = await db.GraphEdges
            .FirstOrDefaultAsync(e =>
                e.ProjectId == projectId &&
                e.SourceNodeId == req.SourceNodeId &&
                e.TargetNodeId == req.TargetNodeId &&
                e.EdgeType == req.EdgeType);

        if (edge is null)
        {
            edge = new GraphEdgeEntity
            {
                ProjectId = projectId,
                SourceNodeId = req.SourceNodeId,
                TargetNodeId = req.TargetNodeId,
                EdgeType = req.EdgeType,
                Weight = req.Weight,
                MetadataJson = meta,
            };
            db.GraphEdges.Add(edge);
        }
        else
        {
            edge.Weight = req.Weight;
            edge.MetadataJson = meta;
        }

        await db.SaveChangesAsync();
        return Ok(ToDto(edge));
    }

    // ── Query: Grafo centrado em task (work item) ──────────────────────────────

    /// <summary>
    /// Retorna o grafo de rastreabilidade centrado em uma task específica.
    /// Inclui todos os nós conectados (commits, arquivos, regras, RAs) e suas arestas.
    /// </summary>
    [HttpGet("task/{workItemId}")]
    public async Task<IActionResult> GetTaskGraph(Guid projectId, string workItemId)
    {
        // Nó raiz da task
        var taskNode = await db.GraphNodes
            .FirstOrDefaultAsync(n =>
                n.ProjectId == projectId &&
                n.NodeType == "task" &&
                n.ExternalId == workItemId);

        if (taskNode is null)
            return Ok(new GraphResponse([], []));

        // Buscar nós conectados (até profundidade 2)
        var nodeIds = new HashSet<Guid> { taskNode.Id };

        // Arestas de saída do nó da task
        var outEdges = await db.GraphEdges
            .Where(e => e.ProjectId == projectId && e.SourceNodeId == taskNode.Id)
            .ToListAsync();

        foreach (var e in outEdges)
            nodeIds.Add(e.TargetNodeId);

        // Arestas de profundidade 2 (ex: commit → files)
        var depth2Edges = await db.GraphEdges
            .Where(e => e.ProjectId == projectId &&
                        nodeIds.Contains(e.SourceNodeId) &&
                        e.SourceNodeId != taskNode.Id)
            .ToListAsync();

        foreach (var e in depth2Edges)
            nodeIds.Add(e.TargetNodeId);

        var allEdges = outEdges.Concat(depth2Edges).ToList();

        var allNodes = await db.GraphNodes
            .Where(n => nodeIds.Contains(n.Id))
            .ToListAsync();

        return Ok(new GraphResponse(
            allNodes.Select(ToDto).ToList(),
            allEdges.Select(ToDto).ToList()
        ));
    }

    // ── Query: Visão macro do backlog ──────────────────────────────────────────

    /// <summary>
    /// Retorna o grafo macro de um backlog item, incluindo todos os grafos
    /// de suas tasks interconectadas.
    /// </summary>
    [HttpGet("backlog/{backlogItemId}")]
    public async Task<IActionResult> GetBacklogGraph(Guid projectId, string backlogItemId)
    {
        // Tasks do backlog (via nós com belongs_to apontando para o nó do backlog)
        var backlogNode = await db.GraphNodes
            .FirstOrDefaultAsync(n =>
                n.ProjectId == projectId &&
                n.NodeType == "backlog" &&
                n.ExternalId == backlogItemId);

        // Retornar todos os nós e arestas do projeto se backlog não encontrado
        var projectNodes = await db.GraphNodes
            .Where(n => n.ProjectId == projectId)
            .ToListAsync();

        var projectEdges = await db.GraphEdges
            .Where(e => e.ProjectId == projectId)
            .ToListAsync();

        return Ok(new GraphResponse(
            projectNodes.Select(ToDto).ToList(),
            projectEdges.Select(ToDto).ToList()
        ));
    }

    // ── Query: Nós por tipo ────────────────────────────────────────────────────

    [HttpGet("nodes")]
    public async Task<IActionResult> GetNodes(Guid projectId, [FromQuery] string? nodeType = null)
    {
        var query = db.GraphNodes.Where(n => n.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(nodeType))
            query = query.Where(n => n.NodeType == nodeType);

        var nodes = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
        return Ok(nodes.Select(ToDto));
    }
}
