using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Services.AzureDevOps;

/// <summary>
/// Synchronizes Briefapp WorkItems to Azure DevOps via REST API v7.2.
/// Uses JSON Patch format (application/json-patch+json) for create/update operations.
/// Authentication: PAT (Personal Access Token) via Basic Auth.
/// </summary>
public class AzureDevOpsSyncService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    AppDbContext db,
    ILogger<AzureDevOpsSyncService> logger)
{
    private const string ApiVersion = "7.1";

    private readonly string _defaultWorkItemType = config["AzureDevOps:DefaultWorkItemType"] ?? "Task";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Synchronizes a single Briefapp WorkItem to Azure DevOps.
    /// Creates a new ADO work item if no mapping exists; updates the existing one otherwise.
    /// </summary>
    public async Task<AzureDevOpsMappingEntity?> SyncWorkItemAsync(ProjectEntity project, WorkItemEntity workItem, int? parentAdoId = null, bool parentIsTask = false, int? sprintAdoId = null, string? sprintName = null, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return null;

        try
        {
            var mapping = await db.AzureDevOpsMappings
                .FirstOrDefaultAsync(m => m.BriefappWorkItemId == workItem.Id, ct);

            if (mapping is null)
            {
                return await CreateWorkItemAsync(project, workItem, parentAdoId, parentIsTask, sprintAdoId, sprintName, ct);
            }
            else
            {
                if (mapping.LastSyncedStatus != workItem.Status ||
                    mapping.LastSyncAt < (workItem.UpdatedAt ?? workItem.CreatedAt) || !string.IsNullOrEmpty(sprintName))
                {
                    return await UpdateWorkItemAsync(project, workItem, mapping, parentAdoId, parentIsTask, sprintAdoId, sprintName, ct);
                }

                return mapping;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to sync WorkItem {WorkItemId}", workItem.Id);
            throw;
        }
    }

    /// <summary>
    /// Synchronizes a generic item (Backlog, Wiki, Doc, Checkpoint) to Azure DevOps.
    /// </summary>
    public async Task<AzureDevOpsMappingEntity?> SyncGenericItemAsync(ProjectEntity project, Guid BriefappId, string type, string title, string description, string tags, string adoItemType = "Issue", CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return null;

        try
        {
            var mapping = await db.AzureDevOpsMappings
                .FirstOrDefaultAsync(m => 
                    (type == "Backlog" && m.BriefappBacklogItemId == BriefappId) ||
                    (type == "Wiki" && m.BriefappWikiId == BriefappId) ||
                    (type == "Doc" && m.BriefappDocumentationId == BriefappId) ||
                    (type == "Checkpoint" && m.BriefappCheckpointId == BriefappId) ||
                    (type == "Sprint" && m.BriefappSprintId == BriefappId), ct);

            if (mapping is null)
            {
                var patchOps = new List<JsonPatchOperation>
                {
                    PatchOp("add", "/fields/System.Title", title),
                    PatchOp("add", "/fields/System.Description", description),
                };

                if (!string.IsNullOrWhiteSpace(tags))
                    patchOps.Add(PatchOp("add", "/fields/System.Tags", tags));

                patchOps.Add(new JsonPatchOperation
                {
                    Op = "add",
                    Path = "/relations/-",
                    Value = new { rel = "Hyperlink", url = $"http://Briefapp.local/{type.ToLower()}s/{BriefappId}", attributes = new { comment = $"Briefapp {type}: {title}" } }
                });

                var url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/${adoItemType}?api-version={ApiVersion}";
                var response = await PatchAsync(project.AdoPat, url, patchOps, ct);

                if (response is null) return null;

                mapping = new AzureDevOpsMappingEntity
                {
                    AzureDevOpsWorkItemId = response.Id,
                    AzureDevOpsUrl = response.Links?.Html?.Href ?? $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_workitems/edit/{response.Id}",
                    LastSyncedRev = response.Rev,
                    LastSyncAt = DateTimeOffset.UtcNow,
                };

                if (type == "Backlog") mapping.BriefappBacklogItemId = BriefappId;
                else if (type == "Wiki") mapping.BriefappWikiId = BriefappId;
                else if (type == "Doc") mapping.BriefappDocumentationId = BriefappId;
                else if (type == "Checkpoint") mapping.BriefappCheckpointId = BriefappId;
                else if (type == "Sprint") mapping.BriefappSprintId = BriefappId;

                db.AzureDevOpsMappings.Add(mapping);
                await db.SaveChangesAsync(ct);
                return mapping;
            }
            else
            {
                var patchOps = new List<JsonPatchOperation>
                {
                    PatchOp("replace", "/fields/System.Title", title),
                    PatchOp("replace", "/fields/System.Description", description)
                };
                if (!string.IsNullOrWhiteSpace(tags)) patchOps.Add(PatchOp("replace", "/fields/System.Tags", tags));

                var url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{mapping.AzureDevOpsWorkItemId}?api-version={ApiVersion}";
                var response = await PatchAsync(project.AdoPat, url, patchOps, ct);

                if (response is not null)
                {
                    mapping.LastSyncedRev = response.Rev;
                    mapping.LastSyncAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                return mapping;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to sync {Type} {Id}", type, BriefappId);
            throw;
        }
    }

    /// <summary>
    /// Ensures that the Briefapp Project Area Path exists in Azure DevOps.
    /// </summary>
    public async Task EnsureAreaPathAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var areaName = project.Name; // E.g. "MyProject"
            var safeProjectName = Uri.EscapeDataString(project.AdoProject);
            var url = $"https://dev.azure.com/{project.AdoOrganization}/{safeProjectName}/_apis/wit/classificationnodes/Areas/{Uri.EscapeDataString(areaName)}?api-version={ApiVersion}";

            var getResp = await client.GetAsync(url, ct);
            if (!getResp.IsSuccessStatusCode)
            {
                // Create it
                var postUrl = $"https://dev.azure.com/{project.AdoOrganization}/{safeProjectName}/_apis/wit/classificationnodes/Areas?api-version={ApiVersion}";
                var body = new { name = areaName };
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                
                var postResp = await client.PostAsync(postUrl, content, ct);
                if (!postResp.IsSuccessStatusCode)
                {
                    var err = await postResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to create Area Path: {postResp.StatusCode} {err}");
                }
                logger.LogInformation("[AzureDevOpsSync] Created Area Path {Area} in project {Project}", areaName, project.AdoProject);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to ensure Area Path for project {Project}", project.Name);
            throw;
        }
    }

    /// <summary>
    /// Ensures the board has the necessary columns (E2E, Homologação, Human in the Loop, Bloqueadas).
    /// </summary>
    public async Task EnsureProjectInfrastructureAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var boardsUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/work/boards?api-version={ApiVersion}";
            var boardsResp = await client.GetAsync(boardsUrl, ct);
            if (!boardsResp.IsSuccessStatusCode)
            {
                var err = await boardsResp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Failed to get boards list: {boardsResp.StatusCode} {err}");
            }
            var boardsJson = await boardsResp.Content.ReadAsStringAsync(ct);
            var boardsNode = JsonNode.Parse(boardsJson);
            var boardsArray = boardsNode?["value"]?.AsArray();
            if (boardsArray == null || boardsArray.Count == 0) return;
            
            var boardName = boardsArray[0]?["name"]?.ToString();
            if (string.IsNullOrEmpty(boardName)) return;

            var url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/work/boards/{Uri.EscapeDataString(boardName)}/columns?api-version={ApiVersion}";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Failed to get board columns: {response.StatusCode} {err}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            var existingColsNode = node?["value"]?.AsArray();
            if (existingColsNode == null) return;

            var existingCols = existingColsNode.Select(c => c?["name"]?.ToString()).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var requiredCols = new[] { "E2E", "Homologação", "Human in the Loop", "Bloqueadas" };
            var colsToAdd = requiredCols.Where(c => !existingCols.Contains(c!)).ToList();

            if (colsToAdd.Any())
            {
                var newColsArray = new JsonArray();
                foreach (var col in existingColsNode) newColsArray.Add(JsonNode.Parse(col!.ToJsonString()));

                foreach (var rc in colsToAdd)
                {
                    newColsArray.Add(new JsonObject
                    {
                        ["name"] = rc,
                        ["columnType"] = "inProgress"
                    });
                }

                var updateContent = new StringContent(newColsArray.ToJsonString(), Encoding.UTF8, "application/json");
                var putResp = await client.PutAsync(url, updateContent, ct);
                
                if (!putResp.IsSuccessStatusCode)
                {
                    var err = await putResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to add columns: {putResp.StatusCode} {err}");
                }
                
                logger.LogInformation("[AzureDevOpsSync] Successfully added required columns to board {Board}", boardName);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to ensure board columns");
            throw;
        }
    }

    /// <summary>
    /// Ensures that specific columns exist in the Azure DevOps board.
    /// </summary>
    public async Task EnsureBoardColumnsAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var boardsUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/work/boards?api-version={ApiVersion}";
            var boardsResponse = await client.GetAsync(boardsUrl, ct);
            if (!boardsResponse.IsSuccessStatusCode)
            {
                var err = await boardsResponse.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Failed to get boards: {boardsResponse.StatusCode} {err}");
            }

            var boardsJson = await boardsResponse.Content.ReadAsStringAsync(ct);
            using var boardsDoc = JsonDocument.Parse(boardsJson);
            var boardsArray = boardsDoc.RootElement.GetProperty("value");

            foreach (var boardElement in boardsArray.EnumerateArray())
            {
                var boardName = boardElement.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(boardName)) continue;

                var safeBoardName = Uri.EscapeDataString(boardName);
                var colsUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/work/boards/{safeBoardName}/columns?api-version={ApiVersion}";
                var colsResponse = await client.GetAsync(colsUrl, ct);
                if (!colsResponse.IsSuccessStatusCode) continue;

                var colsJson = await colsResponse.Content.ReadAsStringAsync(ct);
                var jNode = JsonNode.Parse(colsJson);
                var colsList = jNode?["value"]?.AsArray();
                if (colsList == null) continue;

                var existingNames = colsList
                    .Select(c => c?["name"]?.ToString())
                    .Where(n => n != null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var requiredCols = new[] { "E2E", "Homologação", "Human in the Loop", "Bloqueadas" };
                var colsToAdd = requiredCols.Where(c => !existingNames.Contains(c!)).ToList();

                if (colsToAdd.Count == 0) continue;

                var templateCol = colsList.FirstOrDefault(c => c?["columnType"]?.ToString() == "inProgress") 
                                  ?? colsList.FirstOrDefault(c => c?["columnType"]?.ToString() == "incoming")
                                  ?? colsList.FirstOrDefault();

                if (templateCol == null) continue;

                var templateStateMappings = templateCol["stateMappings"]?.DeepClone();

                int insertIndex = colsList.Count - 1;
                if (insertIndex < 0) insertIndex = 0;

                foreach (var newColName in colsToAdd)
                {
                    var newCol = new JsonObject
                    {
                        ["name"] = newColName,
                        ["columnType"] = "inProgress",
                        ["itemLimit"] = 0
                    };
                    if (templateStateMappings != null)
                    {
                        newCol["stateMappings"] = templateStateMappings.DeepClone();
                    }
                    colsList.Insert(insertIndex, newCol);
                    insertIndex++;
                }

                var putContent = new StringContent(colsList.ToJsonString(), Encoding.UTF8, "application/json");
                var putResponse = await client.PutAsync(colsUrl, putContent, ct);
                
                if (!putResponse.IsSuccessStatusCode)
                {
                    var errorBody = await putResponse.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to update board {boardName} columns: {putResponse.StatusCode} - {errorBody}");
                }
                else
                {
                    logger.LogInformation("[AzureDevOpsSync] Successfully added required columns to board {Board}", boardName);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to ensure board columns");
            throw;
        }
    }

    /// <summary>
    /// Syncs the Briefapp project description to Azure DevOps.
    /// </summary>
    public async Task SyncProjectDescriptionAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;
        if (string.IsNullOrWhiteSpace(project.Description)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var safeProjectName = Uri.EscapeDataString(project.AdoProject);
            
            var getProjectUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/projects/{safeProjectName}?api-version={ApiVersion}";
            var getProjResp = await client.GetAsync(getProjectUrl, ct);
            if (!getProjResp.IsSuccessStatusCode)
            {
                var err = await getProjResp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Failed to get project info: {getProjResp.StatusCode} {err}");
            }
            
            var projJson = await getProjResp.Content.ReadAsStringAsync(ct);
            var projNode = JsonNode.Parse(projJson);
            var projectId = projNode?["id"]?.ToString();
            
            if (string.IsNullOrEmpty(projectId))
                return;

            var url = $"https://dev.azure.com/{project.AdoOrganization}/_apis/projects/{projectId}?api-version={ApiVersion}-preview.4";
            
            var payload = new { description = project.Description };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Failed to sync project description: {response.StatusCode} {body}");
            }
            else
            {
                logger.LogInformation("[AzureDevOpsSync] Synced project description for {Project}", project.AdoProject);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to sync project description");
            throw;
        }
    }

    /// <summary>
    /// Ensures that the default dashboards for Scrum, DORA metrics, and Briefapp insights exist.
    /// Each dashboard gets 10 widgets provisioned via POST /widgets (API v7.1-preview.2).
    /// </summary>
    public async Task EnsureDashboardsAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // Discover team name dynamically (avoids "Projetos Team" hardcoding)
            var teamsUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/projects/{project.AdoProject}/teams?api-version={ApiVersion}-preview.3";
            var teamsResp = await client.GetAsync(teamsUrl, ct);
            if (!teamsResp.IsSuccessStatusCode)
            {
                logger.LogWarning("[AzureDevOpsSync] EnsureDashboards: could not list teams — skipping");
                return;
            }
            var teamsJson = await teamsResp.Content.ReadAsStringAsync(ct);
            var teamsNode = JsonNode.Parse(teamsJson);
            var firstTeam = teamsNode?["value"]?.AsArray()?[0];
            var teamName  = firstTeam?["name"]?.ToString();
            if (string.IsNullOrEmpty(teamName))
            {
                logger.LogWarning("[AzureDevOpsSync] EnsureDashboards: no teams found — skipping");
                return;
            }

            var safeTeamName = Uri.EscapeDataString(teamName);
            var listUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/{safeTeamName}/_apis/dashboard/dashboards?api-version={ApiVersion}-preview.3";
            
            var getResp = await client.GetAsync(listUrl, ct);
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (getResp.IsSuccessStatusCode)
            {
                var json = await getResp.Content.ReadAsStringAsync(ct);
                var node = JsonNode.Parse(json);
                var entries = node?["dashboardEntries"]?.AsArray() ?? node?["value"]?.AsArray();
                if (entries != null)
                    foreach (var entry in entries)
                    {
                        var name = entry?["name"]?.ToString();
                        if (!string.IsNullOrEmpty(name)) existingNames.Add(name);
                    }
            }
            
            var dashboardsToCreate = new List<(string Name, string Description, string Markdown)>
            {
                ("1. Briefapp - Overview", "Visão geral do projeto e progresso do Backlog.",
                    "## Briefapp Overview\n\nCentraliza métricas gerais do projeto geridas pelo **Briefapp Todo List**.\n\n| Dashboard | Foco |\n|---|---|\n| Sprint Atual | Burndown diário |\n| Velocity | Story points por sprint |\n| DORA | Métricas de engenharia |"),
                ("2. Scrum - Sprint Atual", "Acompanhamento da Sprint em andamento.",
                    "## Scrum — Sprint Atual\n\nFoco no acompanhamento diário: burndown, CFD e tarefas bloqueadas."),
                ("3. Scrum - Velocity", "Histórico de velocity das últimas sprints.",
                    "## Scrum — Velocity\n\nAcompanhe a saúde do planejamento e previsibilidade de entregas."),
                ("4. DORA - Deploy Frequency", "Frequência de deploys em produção.",
                    "## DORA: Deployment Frequency\n\nMede com que frequência o código é implantado em produção."),
                ("5. DORA - Lead Time", "Tempo desde o commit até a produção.",
                    "## DORA: Lead Time for Changes\n\nTempo para um commit chegar em produção."),
                ("6. DORA - Time to Restore", "Tempo médio de recuperação (MTTR).",
                    "## DORA: Time to Restore Service\n\nQuanto tempo para restaurar o serviço após uma falha em produção."),
                ("7. DORA - Change Failure Rate", "Taxa de falha nas mudanças.",
                    "## DORA: Change Failure Rate\n\nPorcentagem de deploys que resultaram em falhas em produção."),
                ("8. Briefapp AI - Insights", "Métricas de eficiência e custo do Agente LLM.",
                    "## Briefapp AI Insights\n\nMonitoramento de tokens consumidos, modelos usados e tarefas automatizadas pela IA.")
            };

            var createUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/{safeTeamName}/_apis/dashboard/dashboards?api-version={ApiVersion}-preview.3";

            foreach (var dbInfo in dashboardsToCreate)
            {
                if (existingNames.Contains(dbInfo.Name)) continue;

                var createContent = new StringContent(
                    JsonSerializer.Serialize(new { name = dbInfo.Name, description = dbInfo.Description, refreshInterval = 5 }),
                    Encoding.UTF8, "application/json");
                var createResp = await client.PostAsync(createUrl, createContent, ct);

                if (!createResp.IsSuccessStatusCode)
                {
                    var err = await createResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to create dashboard {dbInfo.Name}: {createResp.StatusCode} {err}");
                }

                var respJson = await createResp.Content.ReadAsStringAsync(ct);
                var respNode = JsonNode.Parse(respJson);
                var dashboardId   = respNode?["id"]?.ToString();
                var dashboardEtag = respNode?["eTag"]?.ToString() ?? "1";

                if (string.IsNullOrEmpty(dashboardId))
                {
                    logger.LogWarning("[AzureDevOpsSync] Dashboard created but no id returned for {Name}", dbInfo.Name);
                    continue;
                }

                logger.LogInformation("[AzureDevOpsSync] Created Dashboard: {Name} (id={Id})", dbInfo.Name, dashboardId);
                await ProvisionDashboardWidgetsAsync(client, project, safeTeamName, dashboardId, dashboardEtag, dbInfo.Name, dbInfo.Markdown, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to ensure dashboards");
            throw;
        }
    }

    /// <summary>
    /// Posts 10 native widgets to a dashboard via POST /widgets (API v7.1-preview.2).
    /// eTag is refreshed after every successful POST to avoid version-conflict errors.
    /// contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.{Suffix}"
    /// </summary>
    private async Task ProvisionDashboardWidgetsAsync(
        HttpClient client, ProjectEntity project, string safeTeamName,
        string dashboardId, string dashboardEtag,
        string dashboardName, string markdownContent, CancellationToken ct)
    {
        const string Prefix = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.";
        var wgtUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/{safeTeamName}/_apis/dashboard/dashboards/{dashboardId}/widgets?api-version={ApiVersion}-preview.2";

        var pieSettings = JsonSerializer.Serialize(new {
            lastArtifactName = "Work Items por Estado",
            transformOptions = new { filter = "State <> 'Closed'", groupBy = "System.State",
                orderBy = new { propertyName = "label", direction = "descending" },
                measure = new { propertyName = "System.Id", aggregation = "count" } },
            userColors = Array.Empty<object>(), chartType = "pieChart",
            scope = new { project = new { name = project.AdoProject }, team = new { name = $"{project.AdoProject} Team" } }
        });
        var barSettings = JsonSerializer.Serialize(new {
            lastArtifactName = "Work Items por Tipo",
            transformOptions = new { filter = "State <> 'Closed'", groupBy = "System.WorkItemType",
                orderBy = new { propertyName = "label", direction = "descending" },
                measure = new { propertyName = "System.Id", aggregation = "count" } },
            userColors = Array.Empty<object>(), chartType = "barChart",
            scope = new { project = new { name = project.AdoProject }, team = new { name = $"{project.AdoProject} Team" } }
        });

        // (name, contributionId-suffix, row, col, rowSpan, colSpan, settings-or-null)
        var widgets = new List<(string Name, string Suffix, int Row, int Col, int RS, int CS, string? Cfg)>
        {
            ("Briefapp — " + dashboardName, "MarkdownWidget",        1, 1, 2, 5,
                JsonSerializer.Serialize(new { content = markdownContent, imageUrl = "" })),
            ("Team Members",                "TeamMembersWidget",     1, 6, 2, 2, null),
            ("Sprint Burndown",             "SprintBurndownWidget",  3, 1, 2, 3, null),
            ("Velocity",                    "VelocityWidget",        3, 4, 2, 3, null),
            ("Cumulative Flow",             "CFDWidget",             3, 7, 2, 4, null),
            ("Work Items por Estado",       "WitChartWidget",        5, 1, 2, 3, pieSettings),
            ("Work Items por Tipo",         "WitChartWidget",        5, 4, 2, 3, barSettings),
            ("Sprint Overview",             "SprintOverviewWidget",  5, 7, 2, 4, null),
            ("Criar Work Item",             "NewWorkItemWidget",     7, 1, 1, 2,
                JsonSerializer.Serialize(new { workItemType = "Task" })),
            ("Links Rapidos",               "OtherLinksWidget",      7, 3, 1, 3, null),
        };

        foreach (var (name, suffix, row, col, rs, cs, cfg) in widgets)
        {
            try
            {
                var body = new JsonObject
                {
                    ["name"]            = name,
                    ["contributionId"]  = Prefix + suffix,
                    ["position"]        = new JsonObject { ["row"] = row, ["column"] = col },
                    ["size"]            = new JsonObject { ["rowSpan"] = rs, ["columnSpan"] = cs },
                    ["settingsVersion"] = new JsonObject { ["major"] = 1, ["minor"] = 0, ["patch"] = 0 },
                    ["dashboard"]       = new JsonObject { ["eTag"] = dashboardEtag }
                };
                if (cfg != null) body["settings"] = cfg;

                var resp = await client.PostAsync(wgtUrl,
                    new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), ct);

                if (resp.IsSuccessStatusCode)
                {
                    var rn = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
                    dashboardEtag = rn?["dashboard"]?["eTag"]?.ToString() ?? dashboardEtag;
                    logger.LogInformation("[AzureDevOpsSync] Widget '{Widget}' added to dashboard {DashId}", name, dashboardId);
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("[AzureDevOpsSync] Widget '{Widget}' failed ({Status}): {Err}",
                        name, resp.StatusCode, err[..Math.Min(200, err.Length)]);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "[AzureDevOpsSync] Exception on widget '{Widget}'", name); }
        }
    }

    /// <summary>
    /// Ensures the ADO team has a valid Area path and backlogIteration configured.
    /// Runs automatically during sync to prevent "no areas selected" and TF400497 errors.
    /// </summary>
    public async Task EnsureTeamSettingsAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // Discover teams dynamically
            var teamsUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/projects/{project.AdoProject}/teams?api-version={ApiVersion}-preview.3";
            var teamsResp = await client.GetAsync(teamsUrl, ct);
            if (!teamsResp.IsSuccessStatusCode) return;
            var teamsJson = await teamsResp.Content.ReadAsStringAsync(ct);
            var teamsNode = JsonNode.Parse(teamsJson);
            var teamsArray = teamsNode?["value"]?.AsArray();
            if (teamsArray == null || teamsArray.Count == 0) return;

            var teamName = teamsArray[0]?["name"]?.ToString();
            var teamId   = teamsArray[0]?["id"]?.ToString();
            if (string.IsNullOrEmpty(teamName) || string.IsNullOrEmpty(teamId)) return;

            var safeTeamName = Uri.EscapeDataString(teamName);

            // 1. Ensure Area path is configured for the team
            var areaValuesUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/{safeTeamName}/_apis/work/teamsettings/teamfieldvalues?api-version={ApiVersion}";
            var areaResp = await client.GetAsync(areaValuesUrl, ct);
            if (areaResp.IsSuccessStatusCode)
            {
                var areaJson = await areaResp.Content.ReadAsStringAsync(ct);
                var areaNode = JsonNode.Parse(areaJson);
                var values   = areaNode?["values"]?.AsArray();

                if (values == null || values.Count == 0)
                {
                    // No area configured — set root project area including children
                    var areaBody = new StringContent(JsonSerializer.Serialize(new
                    {
                        defaultValue = project.AdoProject,
                        values = new[] { new { value = project.AdoProject, includeChildren = true } }
                    }), Encoding.UTF8, "application/json");

                    var patchResp = await client.PatchAsync(areaValuesUrl, areaBody, ct);
                    if (patchResp.IsSuccessStatusCode)
                        logger.LogInformation("[AzureDevOpsSync] Area path configured for team '{Team}'", teamName);
                    else
                    {
                        var err = await patchResp.Content.ReadAsStringAsync(ct);
                        logger.LogWarning("[AzureDevOpsSync] Failed to set Area for team '{Team}': {Status} {Err}", teamName, patchResp.StatusCode, err);
                    }
                }
            }

            // 2. Ensure backlogIteration is set (required to avoid TF400497 on iteration assignment)
            var teamSettingsUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/{safeTeamName}/_apis/work/teamsettings?api-version={ApiVersion}-preview.1";
            var settingsResp = await client.GetAsync(teamSettingsUrl, ct);
            if (settingsResp.IsSuccessStatusCode)
            {
                var settingsJson = await settingsResp.Content.ReadAsStringAsync(ct);
                var settingsNode = JsonNode.Parse(settingsJson);
                var backlogId    = settingsNode?["backlogIteration"]?["id"]?.ToString();

                if (string.IsNullOrEmpty(backlogId) || backlogId == "00000000-0000-0000-0000-000000000000")
                {
                    // Fetch root Iterations node identifier
                    var iterRootUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/classificationnodes/Iterations?api-version={ApiVersion}";
                    var iterRootResp = await client.GetAsync(iterRootUrl, ct);
                    if (iterRootResp.IsSuccessStatusCode)
                    {
                        var iterRootJson = await iterRootResp.Content.ReadAsStringAsync(ct);
                        var iterRootNode = JsonNode.Parse(iterRootJson);
                        var rootIdentifier = iterRootNode?["identifier"]?.ToString();

                        if (!string.IsNullOrEmpty(rootIdentifier))
                        {
                            var patchBody = new StringContent(JsonSerializer.Serialize(new { backlogIteration = rootIdentifier }), Encoding.UTF8, "application/json");
                            var patchResp = await client.PatchAsync(teamSettingsUrl, patchBody, ct);
                            if (patchResp.IsSuccessStatusCode)
                                logger.LogInformation("[AzureDevOpsSync] backlogIteration configured for team '{Team}'", teamName);
                            else
                            {
                                var err = await patchResp.Content.ReadAsStringAsync(ct);
                                logger.LogWarning("[AzureDevOpsSync] Failed to set backlogIteration for team '{Team}': {Status} {Err}", teamName, patchResp.StatusCode, err);
                            }
                        }
                    }
                }
            }

            logger.LogInformation("[AzureDevOpsSync] Team settings verified for '{Team}'", teamName);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to ensure team settings for project {Project}", project.AdoProject);
        }
    }

    /// <summary>
    /// Ensures that a Sprint exists as an Iteration in Azure DevOps and is assigned to the Team.
    /// </summary>
    public async Task EnsureIterationAsync(ProjectEntity project, SprintEntity sprint, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var iterationUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/classificationnodes/Iterations?api-version={ApiVersion}";
            
            // Format dates avoiding nulls or defaults if needed, but ADO accepts ISO8601
            var startDate = sprint.StartDate.ToString("yyyy-MM-dd") + "T00:00:00Z";
            var finishDate = sprint.EndDate.ToString("yyyy-MM-dd") + "T23:59:59Z";
            
            var iterationContent = new StringContent(JsonSerializer.Serialize(new {
                name = sprint.Name,
                attributes = new {
                    startDate = startDate,
                    finishDate = finishDate
                }
            }), Encoding.UTF8, "application/json");

            var iterResponse = await client.PostAsync(iterationUrl, iterationContent, ct);
            string? iterationId = null;

            if (iterResponse.IsSuccessStatusCode)
            {
                var iterJson = await iterResponse.Content.ReadAsStringAsync(ct);
                var iterNode = JsonNode.Parse(iterJson);
                iterationId = iterNode?["identifier"]?.ToString();
            }
            else if (iterResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var getUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/classificationnodes/Iterations/{Uri.EscapeDataString(sprint.Name)}?api-version={ApiVersion}";
                var getResp = await client.GetAsync(getUrl, ct);
                if (getResp.IsSuccessStatusCode)
                {
                    var iterJson = await getResp.Content.ReadAsStringAsync(ct);
                    var iterNode = JsonNode.Parse(iterJson);
                    iterationId = iterNode?["identifier"]?.ToString();
                }
                else
                {
                    var err = await getResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to get existing Iteration: {getResp.StatusCode} {err}");
                }
            }
            else
            {
                var err = await iterResponse.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Failed to create Iteration: {iterResponse.StatusCode} {err}");
            }

            if (!string.IsNullOrEmpty(iterationId))
            {
                var teamsUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/projects/{project.AdoProject}/teams?api-version={ApiVersion}-preview.3";
                var teamsResp = await client.GetAsync(teamsUrl, ct);
                if (!teamsResp.IsSuccessStatusCode)
                {
                    var err = await teamsResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to get teams: {teamsResp.StatusCode} {err}");
                }
                var teamsJson = await teamsResp.Content.ReadAsStringAsync(ct);
                var teamsNode = JsonNode.Parse(teamsJson);
                var teamsArray = teamsNode?["value"]?.AsArray();
                if (teamsArray == null || teamsArray.Count == 0) return;
                
                var teamName = teamsArray[0]?["name"]?.ToString();
                if (string.IsNullOrEmpty(teamName)) return;

                var safeTeamName = Uri.EscapeDataString(teamName);
                var teamUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/{safeTeamName}/_apis/work/teamsettings/iterations?api-version={ApiVersion}";
                var teamContent = new StringContent(JsonSerializer.Serialize(new { id = iterationId }), Encoding.UTF8, "application/json");
                var postTeamResp = await client.PostAsync(teamUrl, teamContent, ct);
                if (!postTeamResp.IsSuccessStatusCode && postTeamResp.StatusCode != System.Net.HttpStatusCode.Conflict)
                {
                    var err = await postTeamResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to associate Iteration with Team: {postTeamResp.StatusCode} {err}");
                }
                logger.LogInformation("[AzureDevOpsSync] Verified Iteration for Sprint {SprintName}", sprint.Name);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to ensure Iteration {SprintName}", sprint.Name);
            throw;
        }
    }

    /// <summary>Tests the connection to Azure DevOps using the provided PAT.</summary>
    public async Task<bool> TestConnectionAsync(string organization, string project, string pat, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(pat)) return false;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // Fetch a single project to verify both Org and Project access
            var url = string.IsNullOrWhiteSpace(project) 
                ? $"https://dev.azure.com/{organization}/_apis/projects?api-version={ApiVersion}"
                : $"https://dev.azure.com/{organization}/_apis/projects/{project}?api-version={ApiVersion}";
                
            var response = await client.GetAsync(url, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("[AzureDevOpsSync] Test connection failed for Org {Org}. Status: {Status}, Body: {Body}", 
                    organization, response.StatusCode, body);
            }
            
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] HTTP request failed during connection test for Org {Org}", organization);
            return false;
        }
    }

    /// <summary>Creates a new Work Item in Azure DevOps and persists the mapping.</summary>
    private async Task<AzureDevOpsMappingEntity?> CreateWorkItemAsync(ProjectEntity project, WorkItemEntity workItem, int? parentAdoId, bool parentIsTask, int? sprintAdoId, string? sprintName, CancellationToken ct)
    {
        var isSubtask = workItem.ParentWorkItemId is not null;
        var adoItemType = isSubtask ? (parentIsTask ? "Task" : _defaultWorkItemType) : _defaultWorkItemType;

        var patchOps = new List<JsonPatchOperation>
        {
            PatchOp("add", "/fields/System.Title", workItem.Title),
            PatchOp("add", "/fields/System.State", MapStatus(workItem.Status)),
        };

        // Build description with tokens consumed metadata
        var descBody = workItem.Description ?? string.Empty;
        if (workItem.TotalTokensSpent > 0 || !string.IsNullOrWhiteSpace(workItem.LastModelUsed))
        {
            descBody += $"\n\n---\n🔢 **Tokens Consumed:** {workItem.TotalTokensSpent:N0}";
            if (!string.IsNullOrWhiteSpace(workItem.LastModelUsed))
                descBody += $"  |  🤖 **Model:** {workItem.LastModelUsed}";
        }
        if (!string.IsNullOrWhiteSpace(descBody))
            patchOps.Add(PatchOp("add", "/fields/System.Description", descBody));

        if (!string.IsNullOrWhiteSpace(sprintName))
            patchOps.Add(PatchOp("add", "/fields/System.IterationPath", $"{project.AdoProject}\\{sprintName}"));

        if (!string.IsNullOrWhiteSpace(workItem.Assignee) && workItem.Assignee.Contains('@'))
            patchOps.Add(PatchOp("add", "/fields/System.AssignedTo", workItem.Assignee));

        if (!string.IsNullOrWhiteSpace(workItem.Tags))
        {
            var tags = workItem.Tags;
            if (workItem.Status == WorkItemStatus.Blocked)
                tags = string.IsNullOrWhiteSpace(tags) ? "Blocked" : $"{tags};Blocked";
            patchOps.Add(PatchOp("add", "/fields/System.Tags", tags));
        }
        else if (workItem.Status == WorkItemStatus.Blocked)
        {
            patchOps.Add(PatchOp("add", "/fields/System.Tags", "Blocked"));
        }

        // Add Briefapp reference as a hyperlink
        patchOps.Add(new JsonPatchOperation
        {
            Op = "add",
            Path = "/relations/-",
            Value = new
            {
                rel = "Hyperlink",
                url = $"http://Briefapp.local/workitems/{workItem.Id}",
                attributes = new { comment = $"Briefapp WorkItem: {workItem.Title}" }
            }
        });

        if (parentAdoId.HasValue)
        {
            // ADO Basic process does not support Task→Task parent-child hierarchy (same category).
            // When parent is also a Task, use 'Related' to avoid the backlog reorder warning.
            var linkType = parentIsTask
                ? "System.LinkTypes.Related"
                : "System.LinkTypes.Hierarchy-Reverse";

            patchOps.Add(new JsonPatchOperation
            {
                Op    = "add",
                Path  = "/relations/-",
                Value = new
                {
                    rel = linkType,
                    url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workItems/{parentAdoId.Value}"
                }
            });
        }

        if (sprintAdoId.HasValue)
        {
            patchOps.Add(new JsonPatchOperation
            {
                Op = "add",
                Path = "/relations/-",
                Value = new
                {
                    rel = "System.LinkTypes.Related",
                    url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workItems/{sprintAdoId.Value}"
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(workItem.Branch) && !string.IsNullOrWhiteSpace(project.GitHubUrl))
        {
            var branchUrl = project.GitHubUrl.TrimEnd('/') + "/tree/" + workItem.Branch;
            patchOps.Add(new JsonPatchOperation
            {
                Op = "add",
                Path = "/relations/-",
                Value = new
                {
                    rel = "Hyperlink",
                    url = branchUrl,
                    attributes = new { comment = "GitHub Branch" }
                }
            });
        }

        // Add commit hyperlinks
        if (workItem.CommitIds.Count > 0 && !string.IsNullOrWhiteSpace(project.GitHubUrl))
        {
            var repoBaseUrl = project.GitHubUrl.TrimEnd('/');
            foreach (var commitId in workItem.CommitIds)
            {
                patchOps.Add(new JsonPatchOperation
                {
                    Op = "add",
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "Hyperlink",
                        url = $"{repoBaseUrl}/commit/{commitId}",
                        attributes = new { comment = $"GitHub Commit: {commitId[..Math.Min(7, commitId.Length)]}" }
                    }
                });
            }
        }

        var url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/${adoItemType}?bypassRules=true&api-version={ApiVersion}";
        var response = await PatchAsync(project.AdoPat!, url, patchOps, ct);

        if (response is null) return null;

        // Upsert — guard against duplicate key on forceAll re-runs or race conditions
        var existing = await db.AzureDevOpsMappings
            .FirstOrDefaultAsync(m => m.BriefappWorkItemId == workItem.Id, ct);

        if (existing is not null)
        {
            existing.AzureDevOpsWorkItemId = response.Id;
            existing.AzureDevOpsUrl        = response.Links?.Html?.Href ?? $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_workitems/edit/{response.Id}";
            existing.LastSyncedRev         = response.Rev;
            existing.LastSyncedStatus      = workItem.Status;
            existing.LastSyncAt            = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[AzureDevOpsSync] Updated (upsert) ADO WorkItem #{AdoId} for Briefapp {BriefappId} — {Title}",
                response.Id, workItem.Id, workItem.Title);
            return existing;
        }

        var mapping = new AzureDevOpsMappingEntity
        {
            BriefappWorkItemId    = workItem.Id,
            AzureDevOpsWorkItemId = response.Id,
            AzureDevOpsUrl        = response.Links?.Html?.Href ?? $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_workitems/edit/{response.Id}",
            LastSyncedRev         = response.Rev,
            LastSyncedStatus      = workItem.Status,
            LastSyncAt            = DateTimeOffset.UtcNow,
        };

        db.AzureDevOpsMappings.Add(mapping);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[AzureDevOpsSync] Created ADO WorkItem #{AdoId} for Briefapp {BriefappId} — {Title}",
            response.Id, workItem.Id, workItem.Title);

        return mapping;
    }

    /// <summary>Updates an existing Work Item in Azure DevOps.</summary>
    private async Task<AzureDevOpsMappingEntity?> UpdateWorkItemAsync(ProjectEntity project, WorkItemEntity workItem, AzureDevOpsMappingEntity mapping, int? parentAdoId, bool parentIsTask, int? sprintAdoId, string? sprintName, CancellationToken ct)
    {
        var patchOps = new List<JsonPatchOperation>
        {
            PatchOp("replace", "/fields/System.Title", workItem.Title),
            PatchOp("replace", "/fields/System.State", MapStatus(workItem.Status)),
        };

        // Build description with tokens consumed metadata
        var descBody = workItem.Description ?? string.Empty;
        if (workItem.TotalTokensSpent > 0 || !string.IsNullOrWhiteSpace(workItem.LastModelUsed))
        {
            descBody += $"\n\n---\n🔢 **Tokens Consumed:** {workItem.TotalTokensSpent:N0}";
            if (!string.IsNullOrWhiteSpace(workItem.LastModelUsed))
                descBody += $"  |  🤖 **Model:** {workItem.LastModelUsed}";
        }
        if (!string.IsNullOrWhiteSpace(descBody))
            patchOps.Add(PatchOp("replace", "/fields/System.Description", descBody));

        if (!string.IsNullOrWhiteSpace(sprintName))
            patchOps.Add(PatchOp("add", "/fields/System.IterationPath", $"{project.AdoProject}\\{sprintName}")); // "add" handles replacing too for paths if previously unset

        if (!string.IsNullOrWhiteSpace(workItem.Assignee) && workItem.Assignee.Contains('@'))
            patchOps.Add(PatchOp("replace", "/fields/System.AssignedTo", workItem.Assignee));

        // Handle tags with Blocked flag
        var tags = workItem.Tags ?? string.Empty;
        if (workItem.Status == WorkItemStatus.Blocked && !tags.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
            tags = string.IsNullOrWhiteSpace(tags) ? "Blocked" : $"{tags};Blocked";
        patchOps.Add(PatchOp("replace", "/fields/System.Tags", tags));

        var url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{mapping.AzureDevOpsWorkItemId}?bypassRules=true&api-version={ApiVersion}";
        var response = await PatchAsync(project.AdoPat!, url, patchOps, ct);

        if (response is null) return mapping;

        // Fix same-category hierarchy: if this is a SubTask (parentIsTask=true),
        // ensure its parent link is 'Related' and not 'Hierarchy-Reverse'.
        if (parentIsTask && parentAdoId.HasValue)
            await FixSubtaskParentLinkAsync(project, mapping.AzureDevOpsWorkItemId, parentAdoId.Value, ct);

        mapping.LastSyncedRev = response.Rev;
        mapping.LastSyncedStatus = workItem.Status;
        mapping.LastSyncAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[AzureDevOpsSync] Updated ADO WorkItem #{AdoId} → State={State} for Briefapp {BriefappId}",
            mapping.AzureDevOpsWorkItemId, MapStatus(workItem.Status), workItem.Id);

        return mapping;
    }

    /// <summary>
    /// Fixes Task→Task same-category hierarchy by removing any Hierarchy-Reverse link
    /// between a subtask and its parent Task, replacing it with a Related link.
    /// This resolves the ADO backlog reorder warning for existing items.
    /// </summary>
    private async Task FixSubtaskParentLinkAsync(ProjectEntity project, int subtaskAdoId, int parentAdoId, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // Fetch current relations of the subtask
            var getUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{subtaskAdoId}?$expand=relations&api-version={ApiVersion}";
            var getResp = await client.GetAsync(getUrl, ct);
            if (!getResp.IsSuccessStatusCode) return;

            var json  = await getResp.Content.ReadAsStringAsync(ct);
            var node  = JsonNode.Parse(json);
            var rels  = node?["relations"]?.AsArray();
            if (rels == null) return;

            var parentUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workItems/{parentAdoId}";
            var fixOps    = new List<JsonPatchOperation>();

            // Find Hierarchy-Reverse links pointing to this parent and mark for removal
            for (int i = rels.Count - 1; i >= 0; i--)
            {
                var rel    = rels[i];
                var relRel = rel?["rel"]?.ToString();
                var relUrl = rel?["url"]?.ToString();

                if (relRel == "System.LinkTypes.Hierarchy-Reverse" &&
                    relUrl != null &&
                    relUrl.TrimEnd('/').EndsWith($"/{parentAdoId}", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the bad parent-child link
                    fixOps.Add(new JsonPatchOperation { Op = "remove", Path = $"/relations/{i}" });

                    // Add the correct Related link
                    fixOps.Add(new JsonPatchOperation
                    {
                        Op    = "add",
                        Path  = "/relations/-",
                        Value = new
                        {
                            rel = "System.LinkTypes.Related",
                            url = parentUrl
                        }
                    });

                    logger.LogInformation(
                        "[AzureDevOpsSync] Fixed Task→Task hierarchy: replaced Hierarchy-Reverse with Related on ADO #{SubId} → #{ParentId}",
                        subtaskAdoId, parentAdoId);
                    break; // only one such link expected
                }
            }

            if (fixOps.Count == 0) return; // already correct or no bad link found

            var patchUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{subtaskAdoId}?bypassRules=true&api-version={ApiVersion}";
            await PatchAsync(project.AdoPat!, patchUrl, fixOps, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to fix subtask parent link for ADO #{SubId}", subtaskAdoId);
        }
    }

    /// <summary>
    /// Maps Briefapp WorkItemStatus to Azure DevOps board column state names.
    /// Supports standard + custom columns (E2E, Homologação, Human in the Loop).
    /// </summary>
    public static string MapStatus(WorkItemStatus status) => status switch
    {
        WorkItemStatus.Todo       => "To Do",
        WorkItemStatus.InProgress => "Doing",
        WorkItemStatus.Review     => "E2E",
        WorkItemStatus.Done       => "Done",
        WorkItemStatus.Blocked    => "To Do",   // ADO "Blocked" is handled via tag; state stays "To Do"
        _                         => "To Do"
    };

    /// <summary>
    /// Maps Azure DevOps state names back to Briefapp WorkItemStatus.
    /// Supports all standard and custom board columns.
    /// </summary>
    public static WorkItemStatus MapAdoStateToBriefapp(string? adoState) => adoState?.Trim() switch
    {
        "To Do"             => WorkItemStatus.Todo,
        "New"               => WorkItemStatus.Todo,
        "Doing"             => WorkItemStatus.InProgress,
        "Active"            => WorkItemStatus.InProgress,
        "In Progress"       => WorkItemStatus.InProgress,
        "E2E"               => WorkItemStatus.Review,
        "Review"            => WorkItemStatus.Review,
        "Homologação"       => WorkItemStatus.Review,
        "Human in the Loop" => WorkItemStatus.Blocked,
        "Bloqueadas"        => WorkItemStatus.Blocked,
        "Blocked"           => WorkItemStatus.Blocked,
        "Done"              => WorkItemStatus.Done,
        "Closed"            => WorkItemStatus.Done,
        "Resolved"          => WorkItemStatus.Done,
        _                   => WorkItemStatus.Todo
    };

    /// <summary>Sends a JSON Patch request to Azure DevOps API.</summary>
    private async Task<AdoWorkItemResponse?> PatchAsync(string pat, string url, List<JsonPatchOperation> patchOps, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");

            // Basic Auth: :{PAT} encoded as Base64
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var json = JsonSerializer.Serialize(patchOps, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

            var response = await client.PatchAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("[AzureDevOpsSync] {Method} {Url} returned {Status}: {Body}",
                    "PATCH", url, (int)response.StatusCode, body);
                throw new HttpRequestException($"[AzureDevOpsSync] PATCH returned {(int)response.StatusCode}: {body}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<AdoWorkItemResponse>(responseJson, JsonOptions);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] HTTP request failed: {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Syncs commit links as Hyperlinks on an existing ADO work item.
    /// Only adds commits not already linked.
    /// </summary>
    public async Task SyncCommitLinksAsync(ProjectEntity project, WorkItemEntity workItem, int adoWorkItemId, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoPat) || workItem.CommitIds.Count == 0) return;
        if (string.IsNullOrWhiteSpace(project.GitHubUrl)) return;

        try
        {
            // First, get the existing relations to avoid duplicates
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var getUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{adoWorkItemId}?$expand=relations&api-version={ApiVersion}";
            var getResp = await client.GetAsync(getUrl, ct);
            var existingUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (getResp.IsSuccessStatusCode)
            {
                var json = await getResp.Content.ReadAsStringAsync(ct);
                var node = JsonNode.Parse(json);
                var relations = node?["relations"]?.AsArray();
                if (relations != null)
                {
                    foreach (var rel in relations)
                    {
                        var url = rel?["url"]?.ToString();
                        if (!string.IsNullOrEmpty(url)) existingUrls.Add(url);
                    }
                }
            }

            var patchOps = new List<JsonPatchOperation>();
            var repoBaseUrl = project.GitHubUrl.TrimEnd('/');

            foreach (var commitId in workItem.CommitIds)
            {
                var commitUrl = $"{repoBaseUrl}/commit/{commitId}";
                if (existingUrls.Contains(commitUrl)) continue;

                patchOps.Add(new JsonPatchOperation
                {
                    Op = "add",
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "Hyperlink",
                        url = commitUrl,
                        attributes = new { comment = $"GitHub Commit: {commitId[..Math.Min(7, commitId.Length)]}" }
                    }
                });
            }

            if (patchOps.Count == 0) return;

            var patchUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{adoWorkItemId}?api-version={ApiVersion}";
            await PatchAsync(project.AdoPat!, patchUrl, patchOps, ct);
            logger.LogInformation("[AzureDevOpsSync] Linked {Count} commits to ADO #{AdoId}", patchOps.Count, adoWorkItemId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to sync commit links for WorkItem {Id}", workItem.Id);
            throw;
        }
    }

    /// <summary>
    /// Syncs all Briefapp Wiki and Documentation pages to the Azure DevOps project Wiki.
    /// Creates the wiki if it doesn't exist, then upserts each page.
    /// </summary>
    public async Task SyncKnowledgeToWikiAsync(ProjectEntity project, List<WikiPageEntity> wikis, List<DocumentationPageEntity> docs, List<KnowledgeCheckpointEntity> checkpoints, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // 1. Ensure project wiki exists
            var wikiId = await EnsureProjectWikiAsync(client, project, ct);
            if (string.IsNullOrEmpty(wikiId))
            {
                logger.LogWarning("[AzureDevOpsSync] Could not create/find project wiki for {Project}", project.AdoProject);
                return;
            }

            // Track created paths to avoid redundant API calls
            var createdPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 2. Sync Wiki Pages
            foreach (var wiki in wikis)
            {
                var pagePath = $"/Briefapp/📘 Wiki/{SanitizeWikiPath(wiki.Category)}/{SanitizeWikiPath(wiki.Title)}";
                var content = $"# 📘 {wiki.Title}\n\n**Type:** Wiki  \n**Category:** {wiki.Category}  \n**Tags:** {wiki.Tags}  \n**Last Updated:** {wiki.UpdatedAt:yyyy-MM-dd HH:mm}\n\n---\n\n{wiki.ContentMarkdown}";
                await EnsureParentPagesAsync(client, project, wikiId, pagePath, createdPaths, ct);
                await UpsertWikiPageAsync(client, project, wikiId, pagePath, content, ct);
            }

            // 3. Sync Documentation Pages
            foreach (var doc in docs)
            {
                var pagePath = $"/Briefapp/📄 Documentation/{SanitizeWikiPath(doc.Category)}/{SanitizeWikiPath(doc.Title)}";
                var content = $"# 📄 {doc.Title}\n\n**Type:** Documentation  \n**Category:** {doc.Category}  \n**Tags:** {doc.Tags}  \n**Last Updated:** {doc.UpdatedAt:yyyy-MM-dd HH:mm}\n\n---\n\n{doc.ContentMarkdown}";
                await EnsureParentPagesAsync(client, project, wikiId, pagePath, createdPaths, ct);
                await UpsertWikiPageAsync(client, project, wikiId, pagePath, content, ct);
            }

            // 4. Sync Knowledge Checkpoints
            foreach (var cp in checkpoints)
            {
                var pagePath = $"/Briefapp/🔖 Checkpoints/{SanitizeWikiPath(cp.Name)}";
                var content = $"# 🔖 {cp.Name}\n\n**Type:** Knowledge Checkpoint  \n**Category:** {cp.Category}  \n**Created:** {cp.CreatedAt:yyyy-MM-dd HH:mm}\n\n---\n\n## Context Snapshot\n{cp.ContextSnapshot}\n\n## Decisions\n{cp.Decisions}\n\n## Risks\n{cp.Risks}\n\n## Next Actions\n{cp.NextActions}";
                await EnsureParentPagesAsync(client, project, wikiId, pagePath, createdPaths, ct);
                await UpsertWikiPageAsync(client, project, wikiId, pagePath, content, ct);
            }

            // 5. Create index page
            var indexContent = $"# 🧠 Briefapp Knowledge Base\n\n**Project:** {project.Name}  \n**Synced at:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n## 📘 Wiki Pages ({wikis.Count})\n\n";
            foreach (var w in wikis)
                indexContent += $"- 📘 [{w.Title}](/Briefapp/📘-Wiki/{SanitizeWikiPath(w.Category)}/{SanitizeWikiPath(w.Title)}) — {w.Category}\n";
            indexContent += $"\n## 📄 Documentation ({docs.Count})\n\n";
            foreach (var d in docs)
                indexContent += $"- 📄 [{d.Title}](/Briefapp/📄-Documentation/{SanitizeWikiPath(d.Category)}/{SanitizeWikiPath(d.Title)}) — {d.Category}\n";
            indexContent += $"\n## 🔖 Checkpoints ({checkpoints.Count})\n\n";
            foreach (var c in checkpoints)
                indexContent += $"- 🔖 [{c.Name}](/Briefapp/🔖-Checkpoints/{SanitizeWikiPath(c.Name)})\n";

            await UpsertWikiPageAsync(client, project, wikiId, "/Briefapp", indexContent, ct);

            logger.LogInformation("[AzureDevOpsSync] Synced {WikiCount} wikis, {DocCount} docs, {CpCount} checkpoints to ADO Wiki",
                wikis.Count, docs.Count, checkpoints.Count);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to sync knowledge to ADO Wiki");
            throw;
        }
    }

    /// <summary>
    /// Ensures all parent pages in the path hierarchy exist before creating a leaf page.
    /// E.g., for path "/Briefapp/Wiki/Architecture/BoxOverview", ensures /Briefapp, /Briefapp/Wiki, /Briefapp/Wiki/Architecture exist.
    /// </summary>
    private async Task EnsureParentPagesAsync(HttpClient client, ProjectEntity project, string wikiId, string pagePath, HashSet<string> createdPaths, CancellationToken ct)
    {
        var parts = pagePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = "";
        // Skip the last part (that's the leaf page itself)
        for (int i = 0; i < parts.Length - 1; i++)
        {
            currentPath += "/" + parts[i];
            if (createdPaths.Contains(currentPath)) continue;
            // Create a simple placeholder page for this intermediate level
            await UpsertWikiPageAsync(client, project, wikiId, currentPath, $"# {parts[i]}", ct);
            createdPaths.Add(currentPath);
        }
    }

    private async Task<string?> EnsureProjectWikiAsync(HttpClient client, ProjectEntity project, CancellationToken ct)
    {
        // List existing wikis
        var listUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wiki/wikis?api-version={ApiVersion}";
        var listResp = await client.GetAsync(listUrl, ct);
        if (listResp.IsSuccessStatusCode)
        {
            var json = await listResp.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            var wikis = node?["value"]?.AsArray();
            if (wikis != null && wikis.Count > 0)
            {
                // Prefer a project wiki (type == "projectWiki")
                var projectWiki = wikis.FirstOrDefault(w => w?["type"]?.ToString() == "projectWiki");
                if (projectWiki != null) return projectWiki["id"]?.ToString();

                // Check for existing "Briefapp" code wiki
                var BriefappWiki = wikis.FirstOrDefault(w => w?["name"]?.ToString() == "Briefapp");
                if (BriefappWiki != null) return BriefappWiki["id"]?.ToString();

                // Use any existing wiki
                return wikis[0]?["id"]?.ToString();
            }
        }

        // Create a code wiki named "Briefapp"
        var createUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wiki/wikis?api-version={ApiVersion}";
        var createBody = new StringContent(JsonSerializer.Serialize(new
        {
            name = "Briefapp",
            type = "projectWiki"
        }), Encoding.UTF8, "application/json");

        var createResp = await client.PostAsync(createUrl, createBody, ct);
        if (createResp.IsSuccessStatusCode)
        {
            var json = await createResp.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            return node?["id"]?.ToString();
        }

        var errBody = await createResp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"[AzureDevOpsSync] Failed to create project wiki: {createResp.StatusCode} {errBody}");
    }

    /// <summary>JSON options that preserve UTF-8 characters (e.g. ã, ç, é) without ASCII-escaping them.</summary>
    private static readonly JsonSerializerOptions WikiJsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private async Task UpsertWikiPageAsync(HttpClient client, ProjectEntity project, string wikiId, string pagePath, string content, CancellationToken ct)
    {
        try
        {
            // ADO Wiki API expects path with / preserved, but other special chars encoded
            // Encode each segment individually, not the whole path
            var segments = pagePath.Split('/').Select(s => string.IsNullOrEmpty(s) ? s : Uri.EscapeDataString(s));
            var encodedPath = string.Join("/", segments);
            var url = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wiki/wikis/{wikiId}/pages?path={encodedPath}&api-version={ApiVersion}";

            // Use WikiJsonOptions to prevent ASCII-escaping of Portuguese/UTF-8 characters.
            // Without this, JsonSerializer converts 'ã' → '\u00e3', which ADO then renders as mojibake.
            var jsonBody = JsonSerializer.Serialize(new { content }, WikiJsonOptions);

            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // First attempt: create new page (no If-Match)
            var response = await client.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Page already exists, update with If-Match: *
                var request2 = new HttpRequestMessage(HttpMethod.Put, url);
                request2.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                request2.Headers.Add("If-Match", "*");
                response = await client.SendAsync(request2, ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"[AzureDevOpsSync] Wiki page upsert {pagePath} failed: {response.StatusCode} {errBody}");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[AzureDevOpsSync] Failed to upsert wiki page {Path}", pagePath);
        }
    }

    private static string SanitizeWikiPath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "General";
        // Remove characters not allowed in wiki paths
        return input.Replace("/", "-").Replace("\\", "-").Replace(":", "-").Replace("?", "").Replace("#", "").Replace("[", "(").Replace("]", ")").Trim();
    }

    /// <summary>
    /// Registers Azure DevOps Service Hook subscriptions so ADO sends webhook events to our API.
    /// Events: workitem.created, workitem.updated, workitem.deleted
    /// </summary>
    public async Task EnsureWebhookSubscriptionsAsync(ProjectEntity project, string webhookBaseUrl, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var callbackUrl = $"{webhookBaseUrl.TrimEnd('/')}/api/azuredevops/webhook";

            // Resolve project name → GUID (Service Hooks API requires GUID for projectId)
            string? projectGuid = null;
            var projectUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/projects/{Uri.EscapeDataString(project.AdoProject)}?api-version={ApiVersion}";
            var projResp = await client.GetAsync(projectUrl, ct);
            if (projResp.IsSuccessStatusCode)
            {
                var projJson = await projResp.Content.ReadAsStringAsync(ct);
                var projNode = JsonNode.Parse(projJson);
                projectGuid = projNode?["id"]?.ToString();
            }
            if (string.IsNullOrEmpty(projectGuid))
            {
                logger.LogWarning("[AzureDevOpsSync] Could not resolve project GUID for {Project}, skipping webhook registration", project.AdoProject);
                return;
            }

            // List existing subscriptions to avoid duplicates
            var listUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/hooks/subscriptions?api-version={ApiVersion}";
            var listResp = await client.GetAsync(listUrl, ct);
            var existingCallbacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (listResp.IsSuccessStatusCode)
            {
                var listJson = await listResp.Content.ReadAsStringAsync(ct);
                var listNode = JsonNode.Parse(listJson);
                var subs = listNode?["value"]?.AsArray();
                if (subs != null)
                {
                    foreach (var sub in subs)
                    {
                        var url = sub?["consumerInputs"]?["url"]?.ToString();
                        var eventType = sub?["eventType"]?.ToString();
                        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(eventType))
                            existingCallbacks.Add($"{eventType}|{url}");
                    }
                }
            }

            var events = new[] { "workitem.created", "workitem.updated", "workitem.deleted" };

            foreach (var eventType in events)
            {
                var key = $"{eventType}|{callbackUrl}";
                if (existingCallbacks.Contains(key))
                {
                    logger.LogDebug("[AzureDevOpsSync] Webhook for {Event} already registered", eventType);
                    continue;
                }

                var subscription = new
                {
                    publisherId = "tfs",
                    eventType = eventType,
                    consumerId = "webHooks",
                    consumerActionId = "httpRequest",
                    publisherInputs = new
                    {
                        projectId = projectGuid
                    },
                    consumerInputs = new
                    {
                        url = callbackUrl
                    }
                };

                var subUrl = $"https://dev.azure.com/{project.AdoOrganization}/_apis/hooks/subscriptions?api-version={ApiVersion}";
                var subContent = new StringContent(JsonSerializer.Serialize(subscription), Encoding.UTF8, "application/json");
                var subResp = await client.PostAsync(subUrl, subContent, ct);

                if (subResp.IsSuccessStatusCode)
                {
                    logger.LogInformation("[AzureDevOpsSync] Registered webhook for {Event} → {Url}", eventType, callbackUrl);
                }
                else
                {
                    var errBody = await subResp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"[AzureDevOpsSync] Failed to register webhook for {eventType}: {subResp.StatusCode} {errBody}");
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to register webhook subscriptions");
            throw;
        }
    }

    private static JsonPatchOperation PatchOp(string op, string path, object value) => new()
    {
        Op = op,
        Path = path,
        Value = value
    };

    /// <summary>
    /// Clears all work items in the project's area path in Azure DevOps.
    /// </summary>
    public async Task ClearProjectBoardAsync(ProjectEntity project, CancellationToken ct = default)
    {
        if (!project.AdoEnabled || string.IsNullOrWhiteSpace(project.AdoOrganization) || string.IsNullOrWhiteSpace(project.AdoProject) || string.IsNullOrWhiteSpace(project.AdoPat)) return;

        try
        {
            var client = httpClientFactory.CreateClient("azuredevops");
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{project.AdoPat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // 1. Query all work items in the Area Path
            var wiqlUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/wiql?api-version={ApiVersion}";
            var areaPath = $"{project.AdoProject}\\{project.Name}";
            var wiqlBody = new { query = $"Select [System.Id] From WorkItems Where [System.AreaPath] = '{areaPath}'" };
            
            var queryResponse = await client.PostAsync(wiqlUrl, new StringContent(JsonSerializer.Serialize(wiqlBody, JsonOptions), Encoding.UTF8, "application/json"), ct);
            if (!queryResponse.IsSuccessStatusCode) return;

            var queryJson = await queryResponse.Content.ReadAsStringAsync(ct);
            var queryNode = JsonNode.Parse(queryJson);
            var workItemsNode = queryNode?["workItems"]?.AsArray();

            if (workItemsNode != null)
            {
                foreach (var item in workItemsNode)
                {
                    var id = item?["id"]?.GetValue<int>();
                    if (id.HasValue)
                    {
                        var deleteUrl = $"https://dev.azure.com/{project.AdoOrganization}/{project.AdoProject}/_apis/wit/workitems/{id.Value}?api-version={ApiVersion}";
                        await client.DeleteAsync(deleteUrl, ct);
                    }
                }
            }

            // Also clear mappings in the DB
            var mappings = await db.AzureDevOpsMappings
                .Include(m => m.BriefappWorkItem)
                .Where(m => m.BriefappWorkItem != null && m.BriefappWorkItem.ProjectId == project.Id)
                .ToListAsync(ct);
            db.AzureDevOpsMappings.RemoveRange(mappings);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AzureDevOpsSync] Failed to clear project board.");
        }
    }

    #region Azure DevOps API DTOs

    internal class JsonPatchOperation
    {
        [JsonPropertyName("op")]
        public string Op { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }

    internal class AdoWorkItemResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("rev")]
        public int Rev { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, JsonElement>? Fields { get; set; }

        [JsonPropertyName("_links")]
        public AdoLinks? Links { get; set; }
    }

    internal class AdoLinks
    {
        [JsonPropertyName("html")]
        public AdoLink? Html { get; set; }
    }

    internal class AdoLink
    {
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;
    }

    #endregion
}
