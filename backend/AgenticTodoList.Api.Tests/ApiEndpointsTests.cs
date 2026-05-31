using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefappTodoList.Api.Contracts;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Tests;

public class ApiEndpointsTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CorsPreflight_ShouldAllowFrontendOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/projects");
        request.Headers.Add("Origin", "http://localhost:8400");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("http://localhost:8400", origins!);
    }

    [Fact]
    public async Task ProjectFlow_ShouldCreateAndListProject()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto VSCode", "Fluxo agentico"));
        createResponse.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/projects");
        Assert.NotNull(list);
        Assert.True(list!.Count > 0);
    }

    [Fact]
    public async Task BacklogAndSprintFlow_ShouldReturnDashboardData()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Scrum", "Descricao")))
            .Content.ReadFromJsonAsync<JsonElement>();

        var projectId = project.GetProperty("id").GetGuid();

        var backlog1Response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/backlog", new AddBacklogItemRequest("Story 1", "Desc", 5, 1));
        backlog1Response.EnsureSuccessStatusCode();
        var backlog1 = await backlog1Response.Content.ReadFromJsonAsync<JsonElement>();

        var backlog2Response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/backlog", new AddBacklogItemRequest("Story 2", "Desc", 3, 2));
        backlog2Response.EnsureSuccessStatusCode();
        var backlog2 = await backlog2Response.Content.ReadFromJsonAsync<JsonElement>();

        var sprintRequest = new
        {
            name = "Sprint 1",
            goal = "Entregar MVP",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            backlogItemIds = new[] { backlog1.GetProperty("id").GetGuid(), backlog2.GetProperty("id").GetGuid() }
        };

        var sprintResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/sprints", sprintRequest);
        sprintResponse.EnsureSuccessStatusCode();

        var dashboardResponse = await _client.GetAsync($"/api/projects/{projectId}/dashboard");
        dashboardResponse.EnsureSuccessStatusCode();

        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, dashboard.GetProperty("backlogTotal").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("activeSprints").GetInt32());
    }

    [Fact]
    public async Task KnowledgeEndpoints_ShouldStoreKnowledgeArtifacts()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Knowledge", "Descricao")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var wikiResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/wiki", new AddWikiPageRequest("Onboarding", "## Steps", "wiki,onboarding", "How-To"));
        wikiResponse.EnsureSuccessStatusCode();

        var checkpointResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/checkpoints", new AddCheckpointRequest("CP-1", "ctx", "dec", "risk", "next", "Release"));
        checkpointResponse.EnsureSuccessStatusCode();

        var docsResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/documentation", new AddDocumentationPageRequest("Arquitetura", "# ADR", "Arquitetura", "adr,design"));
        docsResponse.EnsureSuccessStatusCode();

        var runResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", new AddAgentRunLogRequest("vscode", "mcp", "in", "out", "success", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        runResponse.EnsureSuccessStatusCode();

        var knowledgeResponse = await _client.GetAsync($"/api/projects/{projectId}/knowledge");
        knowledgeResponse.EnsureSuccessStatusCode();

        var knowledge = await knowledgeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, knowledge.GetProperty("wikiPages").GetArrayLength());
        Assert.Equal("How-To", knowledge.GetProperty("wikiPages")[0].GetProperty("category").GetString());
        Assert.Equal(1, knowledge.GetProperty("checkpoints").GetArrayLength());
        Assert.Equal("Release", knowledge.GetProperty("checkpoints")[0].GetProperty("category").GetString());
        Assert.Equal(1, knowledge.GetProperty("documentationPages").GetArrayLength());
        Assert.Equal(1, knowledge.GetProperty("agentRuns").GetArrayLength());
    }

    [Fact]
    public async Task WorkItemStatusUpdates_ShouldAccumulateTokensAndExposeFeedbacks()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Tokens", "Descricao")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var backlogResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/backlog", new AddBacklogItemRequest("Story Token", "Desc", 3, 1));
        backlogResponse.EnsureSuccessStatusCode();
        var backlog = await backlogResponse.Content.ReadFromJsonAsync<JsonElement>();

        var sprintResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/sprints", new
        {
            name = "Sprint Tokens",
            goal = "Validar tracking",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            backlogItemIds = new[] { backlog.GetProperty("id").GetGuid() }
        });
        sprintResponse.EnsureSuccessStatusCode();

        var sprints = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{projectId}/sprints");
        var workItemId = sprints[0].GetProperty("workItems")[0].GetProperty("id").GetGuid();

        var update1 = await _client.PostAsJsonAsync(
            $"/api/work-items/{workItemId}/status",
            new UpdateWorkItemStatusRequest(WorkItemStatus.InProgress, "dev-agent", 100, "copilot", "GPT-5.3-Codex", "VS Code", "Implementacao iniciada", "{}", CommitIds: ["abc111", "def222"]));
        update1.EnsureSuccessStatusCode();

        var update2 = await _client.PostAsJsonAsync(
            $"/api/work-items/{workItemId}/status",
            new UpdateWorkItemStatusRequest(WorkItemStatus.Review, "qa-agent", 50, "copilot", "GPT-5.3-Codex", "VS Code", "Pronto para revisao", "{}"));
        update2.EnsureSuccessStatusCode();

        var updatedSprints = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{projectId}/sprints");
        var workItem = updatedSprints[0].GetProperty("workItems")[0];

        Assert.Equal(150, workItem.GetProperty("totalTokensSpent").GetInt32());
        Assert.Equal("GPT-5.3-Codex", workItem.GetProperty("lastModelUsed").GetString());
        Assert.Equal("VS Code", workItem.GetProperty("lastIdeUsed").GetString());
        Assert.Equal(2, workItem.GetProperty("commitIds").GetArrayLength());
        Assert.Equal(2, workItem.GetProperty("feedbacks").GetArrayLength());
        Assert.Equal(50, workItem.GetProperty("feedbacks")[0].GetProperty("tokensUsed").GetInt32());
    }

    [Fact]
    public async Task ProjectDelete_ShouldArchiveAndSupportIncludeArchivedFlag()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Archive", "Soft delete"));
        createResponse.EnsureSuccessStatusCode();
        var project = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{projectId}");
        deleteResponse.EnsureSuccessStatusCode();

        var activeProjects = await _client.GetFromJsonAsync<List<JsonElement>>("/api/projects");
        Assert.NotNull(activeProjects);
        Assert.DoesNotContain(activeProjects!, p => p.GetProperty("id").GetGuid() == projectId);

        var allProjects = await _client.GetFromJsonAsync<List<JsonElement>>("/api/projects?includeArchived=true");
        Assert.NotNull(allProjects);
        Assert.Contains(allProjects!, p => p.GetProperty("id").GetGuid() == projectId);
    }

    [Fact]
    public async Task ProjectConfig_CreateWithConfig_ShouldPersistConfigFields()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(
            "Projeto Config",
            "Teste de configuracoes",
            GitHubUrl: "https://github.com/qweralfredo/todolist",
            LocalPath: "c:/projetos/todolist",
            TechStack: ".NET 10, React, PostgreSQL",
            MainBranch: "main"));
        createResponse.EnsureSuccessStatusCode();

        var project = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/projects");
        var found = list!.First(p => p.GetProperty("id").GetGuid() == projectId);

        Assert.Equal("https://github.com/qweralfredo/todolist", found.GetProperty("gitHubUrl").GetString());
        Assert.Equal("c:/projetos/todolist", found.GetProperty("localPath").GetString());
        Assert.Equal(".NET 10, React, PostgreSQL", found.GetProperty("techStack").GetString());
        Assert.Equal("main", found.GetProperty("mainBranch").GetString());
    }

    [Fact]
    public async Task ProjectConfig_PatchConfig_ShouldUpdateConfigFields()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Patch Config", "Desc")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var patchResponse = await _client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/config",
            new UpdateProjectConfigRequest(
                GitHubUrl: "https://github.com/org/repo",
                LocalPath: "c:/projetos/repo",
                TechStack: "Python, FastAPI",
                MainBranch: "develop"));
        patchResponse.EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/projects");
        var updated = list!.First(p => p.GetProperty("id").GetGuid() == projectId);

        Assert.Equal("https://github.com/org/repo", updated.GetProperty("gitHubUrl").GetString());
        Assert.Equal("c:/projetos/repo", updated.GetProperty("localPath").GetString());
        Assert.Equal("Python, FastAPI", updated.GetProperty("techStack").GetString());
        Assert.Equal("develop", updated.GetProperty("mainBranch").GetString());
    }

    [Fact]
    public async Task ProjectConfig_PatchConfigOnUnknownProject_ShouldReturn404()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/config",
            new UpdateProjectConfigRequest("https://github.com/x", null, null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubTasks_ShouldCreateAndExposeParentWorkItemId()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto SubTasks", "Desc")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var backlogResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/backlog", new AddBacklogItemRequest("Story", "Desc", 5, 1));
        backlogResponse.EnsureSuccessStatusCode();
        var backlog = await backlogResponse.Content.ReadFromJsonAsync<JsonElement>();

        var sprintResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/sprints", new
        {
            name = "Sprint Sub",
            goal = "Sub-task test",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            backlogItemIds = new[] { backlog.GetProperty("id").GetGuid() }
        });
        sprintResponse.EnsureSuccessStatusCode();

        var sprints = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{projectId}/sprints");
        var parentId = sprints[0].GetProperty("workItems")[0].GetProperty("id").GetGuid();

        var subResponse = await _client.PostAsJsonAsync($"/api/work-items/{parentId}/sub-tasks",
            new AddSubTaskRequest("Sub A", "Sub desc", "dev", "feature/sub-a", "sub"));
        Assert.Equal(HttpStatusCode.Created, subResponse.StatusCode);

        var sub = await subResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(parentId, sub.GetProperty("parentWorkItemId").GetGuid());
        Assert.Equal("feature/sub-a", sub.GetProperty("branch").GetString());
    }

    [Fact]
    public async Task BacklogContext_PatchContext_ShouldUpdateTagsAndWikiRefs()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Context", "Desc")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var backlogResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/backlog", new AddBacklogItemRequest("Story", "Desc", 5, 1));
        backlogResponse.EnsureSuccessStatusCode();
        var backlog = await backlogResponse.Content.ReadFromJsonAsync<JsonElement>();
        var backlogId = backlog.GetProperty("id").GetGuid();

        var patchResponse = await _client.PatchAsJsonAsync(
            $"/api/backlog-items/{backlogId}/context",
            new UpdateBacklogItemContextRequest("tag1,tag2", "wiki:Onboarding", "Must be before release"));
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updated = await patchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tag1,tag2", updated.GetProperty("tags").GetString());
        Assert.Equal("wiki:Onboarding", updated.GetProperty("wikiRefs").GetString());
    }

    [Fact]
    public async Task CommitIds_ShouldBeAppendedOnBacklogAndSprintEndpoints()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Projeto Commits", "Desc")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var backlogResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/backlog",
            new AddBacklogItemRequest("Story", "Desc", 5, 1, ["abc111"]));
        backlogResponse.EnsureSuccessStatusCode();
        var backlog = await backlogResponse.Content.ReadFromJsonAsync<JsonElement>();
        var backlogId = backlog.GetProperty("id").GetGuid();

        var sprintResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/sprints", new
        {
            name = "Sprint Commits",
            goal = "Track commit ids",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            backlogItemIds = new[] { backlogId },
            commitIds = new[] { "spr001" }
        });
        sprintResponse.EnsureSuccessStatusCode();
        var sprint = await sprintResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sprintId = sprint.GetProperty("id").GetGuid();

        var patchBacklog = await _client.PatchAsJsonAsync(
            $"/api/backlog-items/{backlogId}/context",
            new UpdateBacklogItemContextRequest(null, null, null, ["def222", "abc111"]));
        patchBacklog.EnsureSuccessStatusCode();
        var updatedBacklog = await patchBacklog.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updatedBacklog.GetProperty("commitIds").GetArrayLength());

        var patchSprint = await _client.PatchAsJsonAsync(
            $"/api/sprints/{sprintId}/commits",
            new UpdateSprintCommitIdsRequest(["spr001", "spr002"]));
        patchSprint.EnsureSuccessStatusCode();
        var updatedSprint = await patchSprint.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updatedSprint.GetProperty("commitIds").GetArrayLength());
    }

    // ---- SP-03: Agent Metrics + Human Evaluation tests ----

    [Fact]
    public async Task AgentRun_WithMetrics_ShouldPersistDevLakeFields()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Metrics Project", "DevLake SP-03")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var runResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", new
        {
            agentName = "claude-code",
            entryPoint = "feat/sp03",
            inputSummary = "input",
            outputSummary = "output",
            status = "done",
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            finishedAt = DateTimeOffset.UtcNow,
            modelName = "claude-sonnet-4-6",
            tokensInput = 1000,
            tokensOutput = 500,
            latencyMs = 3200,
            costUsd = 0.015,
            success = true,
            environment = "production"
        });
        Assert.Equal(HttpStatusCode.Created, runResponse.StatusCode);

        var run = await runResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("claude-sonnet-4-6", run.GetProperty("modelName").GetString());
        Assert.Equal(1000, run.GetProperty("tokensInput").GetInt32());
        Assert.Equal(500, run.GetProperty("tokensOutput").GetInt32());
        Assert.Equal(3200, run.GetProperty("latencyMs").GetInt64());
        Assert.True(run.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task HumanEvaluation_Submit_ShouldComputeCompositeScore()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Eval Project", "Human eval SP-03")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var runResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", new
        {
            agentName = "evaluator-test",
            entryPoint = "eval",
            inputSummary = "q",
            outputSummary = "a",
            status = "done",
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            finishedAt = DateTimeOffset.UtcNow,
            modelName = "claude-haiku-4-5",
            success = true
        });
        var run = await runResponse.Content.ReadFromJsonAsync<JsonElement>();
        var runId = run.GetProperty("id").GetGuid();

        // accuracy=1.0 (30%) + relevance=0.8 (25%) + completeness=0.6 (25%) + safety=1.0 (20%)
        // = (0.30 + 0.20 + 0.15 + 0.20) * 5 = 0.85 * 5 = 4.25
        var evalResponse = await _client.PostAsJsonAsync($"/api/agent-runs/{runId}/evaluations", new
        {
            reviewerId = "reviewer-alice",
            accuracyScore = 1.0f,
            relevanceScore = 0.8f,
            completenessScore = 0.6f,
            safetyScore = 1.0f,
            feedbackText = "Good overall",
            requiresEscalation = false,
            reviewTimeSeconds = 120
        });
        Assert.Equal(HttpStatusCode.Created, evalResponse.StatusCode);

        var eval = await evalResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("reviewer-alice", eval.GetProperty("reviewerId").GetString());
        var score = eval.GetProperty("score").GetSingle();
        Assert.True(score > 4.0f && score < 4.5f, $"Expected score ~4.25, got {score}");
    }

    [Fact]
    public async Task HumanEvaluation_List_ShouldReturnEvaluationsForRun()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("List Eval Project", "SP-03 list test")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var runResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", new
        {
            agentName = "list-test-agent",
            entryPoint = "list",
            inputSummary = "i",
            outputSummary = "o",
            status = "done",
            startedAt = DateTimeOffset.UtcNow,
            success = true
        });
        var run = await runResponse.Content.ReadFromJsonAsync<JsonElement>();
        var runId = run.GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/agent-runs/{runId}/evaluations", new
        {
            reviewerId = "reviewer-bob",
            accuracyScore = 0.8f,
            relevanceScore = 0.8f,
            completenessScore = 0.8f,
            safetyScore = 0.8f
        });

        var listResponse = await _client.GetAsync($"/api/agent-runs/{runId}/evaluations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var evals = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(evals);
        Assert.True(evals!.Count >= 1);
    }

    [Fact]
    public async Task HumanEvaluation_NotFound_RunId_ShouldReturn404()
    {
        var fakeRunId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync($"/api/agent-runs/{fakeRunId}/evaluations", new
        {
            reviewerId = "nobody",
            accuracyScore = 0.5f,
            relevanceScore = 0.5f,
            completenessScore = 0.5f,
            safetyScore = 0.5f
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PendingEvaluations_ShouldReturnRunsWithoutReview()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Pending Eval Project", "SP-03 pending")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/projects/{projectId}/agent-runs", new
        {
            agentName = "unevaluated-agent",
            entryPoint = "task",
            inputSummary = "i",
            outputSummary = "o",
            status = "done",
            startedAt = DateTimeOffset.UtcNow,
            success = true
        });

        var pendingResponse = await _client.GetAsync($"/api/projects/{projectId}/evaluations/pending");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);

        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(pending);
        Assert.True(pending!.Count >= 1);
    }

}

