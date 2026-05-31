using BriefappTodoList.Api.Contracts;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Tests;

public class DomainContractsDefaultsTests
{
    [Fact]
    public void DomainEntities_ShouldInitializeDefaultValues()
    {
        var project = new ProjectEntity();
        var backlog = new BacklogItemEntity();
        var sprint = new SprintEntity();
        var workItem = new WorkItemEntity();
        var feedback = new WorkItemFeedbackEntity();
        var review = new ReviewEntity();
        var wiki = new WikiPageEntity();
        var doc = new DocumentationPageEntity();
        var checkpoint = new KnowledgeCheckpointEntity();
        var run = new AgentRunLogEntity();

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.NotEqual(Guid.Empty, backlog.Id);
        Assert.NotEqual(Guid.Empty, sprint.Id);
        Assert.NotEqual(Guid.Empty, workItem.Id);
        Assert.NotEqual(Guid.Empty, feedback.Id);
        Assert.NotEqual(Guid.Empty, review.Id);
        Assert.NotEqual(Guid.Empty, wiki.Id);
        Assert.NotEqual(Guid.Empty, doc.Id);
        Assert.NotEqual(Guid.Empty, checkpoint.Id);
        Assert.NotEqual(Guid.Empty, run.Id);

        Assert.Equal(string.Empty, project.Name);
        Assert.Equal(string.Empty, backlog.Title);
        Assert.Equal(string.Empty, sprint.Name);
        Assert.Equal(string.Empty, workItem.Title);
        Assert.Equal(string.Empty, feedback.AgentName);
        Assert.Equal(string.Empty, review.Type);
        Assert.Equal(string.Empty, wiki.Title);
        Assert.Equal(string.Empty, doc.Title);
        Assert.Equal(string.Empty, checkpoint.Name);
        Assert.Equal(string.Empty, run.AgentName);
        Assert.Empty(backlog.CommitIds);
        Assert.Empty(sprint.CommitIds);
        Assert.Empty(workItem.CommitIds);

        Assert.Equal(BacklogItemStatus.New, backlog.Status);
        Assert.Equal(WorkItemStatus.Todo, workItem.Status);
        Assert.Equal(SprintStatus.Planned, sprint.Status);
    }

    [Fact]
    public void DtoRecords_ShouldBindValues()
    {
        var createProject = new CreateProjectRequest("N", "D");
        var backlog = new AddBacklogItemRequest("T", "D", 3, 1, ["abc123", "def456"]);
        var sprint = new CreateSprintRequest("S", "G", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), [], ["fedcba"]);
        var update = new UpdateWorkItemStatusRequest(WorkItemStatus.Done, "agent", 123, "copilot", "GPT-5.3-Codex", "VS Code", "ok", "{}", CommitIds: ["abc123"]);
        var sprintCommits = new UpdateSprintCommitIdsRequest(["123abcd", "456efgh"]);
        var review = new AddReviewRequest("review", "sum", "notes");
        var wiki = new AddWikiPageRequest("wiki", "body", "tag");
        var checkpoint = new AddCheckpointRequest("cp", "ctx", "dec", "risk", "next");
        var documentation = new AddDocumentationPageRequest("adr", "body", "Architecture", "tag");
        var run = new AddAgentRunLogRequest("a", "mcp", "in", "out", "ok", DateTimeOffset.UtcNow, null);
        var dashboard = new DashboardDto(Guid.NewGuid(), "Proj", 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

        Assert.Equal("N", createProject.Name);
        Assert.Equal(3, backlog.StoryPoints);
        Assert.Equal("S", sprint.Name);
        Assert.Equal(WorkItemStatus.Done, update.Status);
        Assert.Equal(123, update.TokensUsed);
        Assert.Equal("GPT-5.3-Codex", update.ModelUsed);
        Assert.Equal("VS Code", update.IdeUsed);
        Assert.Equal(2, backlog.CommitIds!.Length);
        Assert.Equal("fedcba", sprint.CommitIds![0]);
        Assert.Equal("abc123", update.CommitIds![0]);
        Assert.Equal(2, sprintCommits.CommitIds.Length);
        Assert.Equal("review", review.Type);
        Assert.Equal("wiki", wiki.Title);
        Assert.Equal("cp", checkpoint.Name);
        Assert.Equal("Architecture", documentation.Category);
        Assert.Equal("a", run.AgentName);
        Assert.Equal("Proj", dashboard.ProjectName);
    }

    [Fact]
    public void Enums_ShouldExposeExpectedValues()
    {
        Assert.Equal(0, (int)BacklogItemStatus.New);
        Assert.Equal(4, (int)BacklogItemStatus.Blocked);
        Assert.Equal(0, (int)WorkItemStatus.Todo);
        Assert.Equal(4, (int)WorkItemStatus.Blocked);
        Assert.Equal(0, (int)SprintStatus.Planned);
        Assert.Equal(2, (int)SprintStatus.Closed);
    }
}

