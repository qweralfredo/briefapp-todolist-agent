using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefappTodoList.Api.Contracts;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Tests;

public class ApiErrorPathsTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public ApiErrorPathsTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_ForMissingProject_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/dashboard");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddBacklog_ForMissingProject_ShouldReturnNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/backlog",
            new AddBacklogItemRequest("Story", "Desc", 3, 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSprint_ForMissingProject_ShouldReturnNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/sprints",
            new
            {
                name = "Sprint X",
                goal = "Goal",
                startDate = DateOnly.FromDateTime(DateTime.UtcNow),
                endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                backlogItemIds = Array.Empty<Guid>()
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_ForMissingWorkItem_ShouldReturnNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/work-items/{Guid.NewGuid()}/status",
            new UpdateWorkItemStatusRequest(WorkItemStatus.InProgress, "agent-2"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddReview_ForMissingSprint_ShouldReturnNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/sprints/{Guid.NewGuid()}/reviews",
            new AddReviewRequest("retro", "sum", "notes"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task KnowledgeWrites_ForMissingProject_ShouldReturnNotFound()
    {
        var projectId = Guid.NewGuid();

        var wikiResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/wiki",
            new AddWikiPageRequest("Wiki", "Body", "tag1"));

        var checkpointResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/checkpoints",
            new AddCheckpointRequest("CP", "ctx", "dec", "risk", "next"));

        var runResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/agent-runs",
            new AddAgentRunLogRequest("agent", "mcp", "in", "out", "ok", DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.NotFound, wikiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, checkpointResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, runResponse.StatusCode);
    }

    [Fact]
    public async Task WorkItemAndReview_HappyPath_ShouldReturnCreatedAndOk()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("P", "D")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        var backlog = await (await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/backlog",
            new AddBacklogItemRequest("Story", "Desc", 2, 1))).Content.ReadFromJsonAsync<JsonElement>();

        var sprintResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/sprints",
            new
            {
                name = "Sprint",
                goal = "Goal",
                startDate = DateOnly.FromDateTime(DateTime.UtcNow),
                endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                backlogItemIds = new[] { backlog.GetProperty("id").GetGuid() }
            });
        sprintResponse.EnsureSuccessStatusCode();

        var sprints = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{projectId}/sprints");
        var sprintId = sprints[0].GetProperty("id").GetGuid();
        var workItemId = sprints[0].GetProperty("workItems")[0].GetProperty("id").GetGuid();

        var updateResponse = await _client.PostAsJsonAsync(
            $"/api/work-items/{workItemId}/status",
            new UpdateWorkItemStatusRequest(WorkItemStatus.Review, "qa-agent"));

        var reviewResponse = await _client.PostAsJsonAsync(
            $"/api/sprints/{sprintId}/reviews",
            new AddReviewRequest("review", "summary", "notes"));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
    }
}

