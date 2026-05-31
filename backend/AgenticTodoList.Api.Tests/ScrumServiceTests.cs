using BriefappTodoList.Api.Contracts;
using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain;
using BriefappTodoList.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BriefappTodoList.Api.Tests;

public class ScrumServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateProjectAndBacklog_ShouldPersistData()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Briefapp Core", "Plataforma scrum"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story A", "Descricao", 5, 1), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal(project.Id, backlog.ProjectId);
        Assert.Equal(BacklogItemStatus.Planned, backlog.Status);
    }

    [Fact]
    public async Task CreateSprint_ShouldCreateWorkItemsAndMarkBacklogInSprint()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Briefapp", "Descricao"), CancellationToken.None);
        var b1 = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Task 1", "Desc", 3, 1), CancellationToken.None);
        var b2 = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Task 2", "Desc", 2, 2), CancellationToken.None);

        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("Sprint 1", "Entregar MVP", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)), [b1.Id, b2.Id]),
            CancellationToken.None);

        var workItems = await db.WorkItems.Where(w => w.SprintId == sprint.Id).ToListAsync();
        Assert.Equal(2, workItems.Count);
        Assert.All(workItems, w => Assert.Equal(WorkItemStatus.Todo, w.Status));

        var reloaded = await db.BacklogItems.Where(b => b.ProjectId == project.Id).ToListAsync();
        Assert.All(reloaded, b => Assert.Equal(BacklogItemStatus.InSprint, b.Status));
    }

    [Fact]
    public async Task UpdateWorkItemStatus_Done_ShouldMarkBacklogDone()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 8, 1), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("Sprint", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id]),
            CancellationToken.None);

        var item = await db.WorkItems.FirstAsync(w => w.SprintId == sprint.Id);
        var updated = await service.UpdateWorkItemStatusAsync(
            item.Id,
            new UpdateWorkItemStatusRequest(WorkItemStatus.Done, "agent-1", 150, "copilot", "GPT-5.3-Codex", "VS Code", "Task finalizada", "{\"source\":\"mcp\"}"),
            CancellationToken.None);

        Assert.Equal(WorkItemStatus.Done, updated.Status);
        Assert.Equal("agent-1", updated.Assignee);
        Assert.Equal(150, updated.TotalTokensSpent);
        Assert.Equal("GPT-5.3-Codex", updated.LastModelUsed);
        Assert.Equal("VS Code", updated.LastIdeUsed);

        var backlogReloaded = await db.BacklogItems.FirstAsync(b => b.Id == backlog.Id);
        Assert.Equal(BacklogItemStatus.Done, backlogReloaded.Status);

        var feedbacks = await db.WorkItemFeedbacks.Where(f => f.WorkItemId == item.Id).ToListAsync();
        Assert.Single(feedbacks);
        Assert.Equal(150, feedbacks[0].TokensUsed);
        Assert.Equal("copilot", feedbacks[0].AgentName);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_MultipleUpdates_ShouldAccumulateTokensAndAppendFeedbacks()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("Sprint", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id]),
            CancellationToken.None);

        var item = await db.WorkItems.FirstAsync(w => w.SprintId == sprint.Id);

        await service.UpdateWorkItemStatusAsync(item.Id, new UpdateWorkItemStatusRequest(WorkItemStatus.InProgress, "dev-agent", 120, "copilot", "GPT-5.3-Codex", "VS Code", "Iniciando", "{}"), CancellationToken.None);
        var updated = await service.UpdateWorkItemStatusAsync(item.Id, new UpdateWorkItemStatusRequest(WorkItemStatus.Review, "qa-agent", 80, "copilot", "GPT-5.3-Codex", "VS Code", "Pronto para QA", "{}"), CancellationToken.None);

        Assert.Equal(200, updated.TotalTokensSpent);
        Assert.Equal("GPT-5.3-Codex", updated.LastModelUsed);

        var feedbacks = await db.WorkItemFeedbacks
            .Where(f => f.WorkItemId == item.Id)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, feedbacks.Count);
        Assert.Equal(120, feedbacks[0].TokensUsed);
        Assert.Equal(80, feedbacks[1].TokensUsed);
    }

    [Fact]
    public async Task KnowledgeAndAgentRun_ShouldBeIncludedInDashboard()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Knowledge", "Desc"), CancellationToken.None);
        await service.AddWikiPageAsync(project.Id, new AddWikiPageRequest("Onboarding", "## Steps", "onboarding,docs", "How-To"), CancellationToken.None);
        await service.AddCheckpointAsync(project.Id, new AddCheckpointRequest("Checkpoint 1", "Contexto", "Decisao", "Risco", "Proximas acoes", "Arquitetura"), CancellationToken.None);
        await service.AddDocumentationPageAsync(project.Id, new AddDocumentationPageRequest("ADR-0001", "# Contexto", "Arquitetura", "adr"), CancellationToken.None);
        await service.AddAgentRunAsync(project.Id, new AddAgentRunLogRequest("vscode-agent", "mcp", "entrada", "saida", "success", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);

        var dashboard = await service.GetDashboardAsync(project.Id, CancellationToken.None);

        Assert.Equal(1, dashboard.KnowledgeCheckpoints);
        Assert.Equal(1, dashboard.WikiPages);
        Assert.Equal(1, dashboard.AgentRuns);

        var docs = await db.DocumentationPages.Where(d => d.ProjectId == project.Id).ToListAsync();
        Assert.Single(docs);
        Assert.Equal("Arquitetura", docs[0].Category);
    }

    [Fact]
    public async Task AddSubTask_ShouldCreateWithParentWorkItemId()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("S1", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id]),
            CancellationToken.None);

        var parent = await db.WorkItems.FirstAsync(w => w.SprintId == sprint.Id);
        var subTask = await service.AddSubTaskAsync(
            parent.Id,
            new AddSubTaskRequest("Sub-task A", "Sub desc", "dev-1", "feature/sub-a", "sub,alpha"),
            CancellationToken.None);

        Assert.Equal(parent.Id, subTask.ParentWorkItemId);
        Assert.Equal(parent.SprintId, subTask.SprintId);
        Assert.Equal(parent.BacklogItemId, subTask.BacklogItemId);
        Assert.Equal("feature/sub-a", subTask.Branch);
        Assert.Equal("sub,alpha", subTask.Tags);
        Assert.Equal(WorkItemStatus.Todo, subTask.Status);
    }

    [Fact]
    public async Task AddSubTask_AllDone_ShouldAutoCompleteParent()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("S1", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id]),
            CancellationToken.None);

        var parent = await db.WorkItems.FirstAsync(w => w.SprintId == sprint.Id);
        var sub1 = await service.AddSubTaskAsync(parent.Id, new AddSubTaskRequest("Sub 1", "Desc"), CancellationToken.None);
        var sub2 = await service.AddSubTaskAsync(parent.Id, new AddSubTaskRequest("Sub 2", "Desc"), CancellationToken.None);

        await service.UpdateWorkItemStatusAsync(sub1.Id, new UpdateWorkItemStatusRequest(WorkItemStatus.Done, "a"), CancellationToken.None);
        await service.UpdateWorkItemStatusAsync(sub2.Id, new UpdateWorkItemStatusRequest(WorkItemStatus.Done, "a"), CancellationToken.None);

        var parentReloaded = await db.WorkItems.FindAsync(parent.Id);
        Assert.Equal(WorkItemStatus.Done, parentReloaded!.Status);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_ShouldPersistBranch()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("S1", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id]),
            CancellationToken.None);

        var item = await db.WorkItems.FirstAsync(w => w.SprintId == sprint.Id);
        var updated = await service.UpdateWorkItemStatusAsync(
            item.Id,
            new UpdateWorkItemStatusRequest(WorkItemStatus.InProgress, "dev", Branch: "feature/context-first"),
            CancellationToken.None);

        Assert.Equal("feature/context-first", updated.Branch);
    }

    [Fact]
    public async Task UpdateBacklogItemContext_ShouldUpdateTagsWikiRefsConstraints()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1), CancellationToken.None);

        var updated = await service.UpdateBacklogItemContextAsync(
            backlog.Id,
            new UpdateBacklogItemContextRequest("tag1,tag2", "wiki:Onboarding", "Must be done before release"),
            CancellationToken.None);

        Assert.Equal("tag1,tag2", updated.Tags);
        Assert.Equal("wiki:Onboarding", updated.WikiRefs);
        Assert.Equal("Must be done before release", updated.Constraints);
    }

    [Fact]
    public async Task UpdateWorkItemStatus_WithCommitIds_ShouldAppendAndPropagateToBacklogAndSprint()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1, ["abc111"]), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("S1", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id], ["spr001"]),
            CancellationToken.None);

        var item = await db.WorkItems.FirstAsync(w => w.SprintId == sprint.Id);

        var updated = await service.UpdateWorkItemStatusAsync(
            item.Id,
            new UpdateWorkItemStatusRequest(WorkItemStatus.InProgress, "dev", 0, CommitIds: ["abc111", "def222", "DEF222"]),
            CancellationToken.None);

        var sprintReloaded = await db.Sprints.FirstAsync(s => s.Id == sprint.Id);
        var backlogReloaded = await db.BacklogItems.FirstAsync(b => b.Id == backlog.Id);

        Assert.Equal(2, updated.CommitIds.Count);
        Assert.Contains("abc111", updated.CommitIds);
        Assert.Contains("def222", updated.CommitIds, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(3, sprintReloaded.CommitIds.Count);
        Assert.Contains("spr001", sprintReloaded.CommitIds);
        Assert.Contains("def222", sprintReloaded.CommitIds, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, backlogReloaded.CommitIds.Count);
        Assert.Contains("abc111", backlogReloaded.CommitIds);
        Assert.Contains("def222", backlogReloaded.CommitIds, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateBacklogItemContext_WithCommitIds_ShouldAppendUniqueValues()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1, ["abc111"]), CancellationToken.None);

        var updated = await service.UpdateBacklogItemContextAsync(
            backlog.Id,
            new UpdateBacklogItemContextRequest(null, null, null, ["abc111", "ghi333"]),
            CancellationToken.None);

        Assert.Equal(2, updated.CommitIds.Count);
        Assert.Contains("abc111", updated.CommitIds);
        Assert.Contains("ghi333", updated.CommitIds);
    }

    [Fact]
    public async Task UpdateSprintCommitIds_ShouldAppendUniqueValues()
    {
        await using var db = CreateDbContext();
        var service = new ScrumService(db);

        var project = await service.CreateProjectAsync(new CreateProjectRequest("Project", "Desc"), CancellationToken.None);
        var backlog = await service.AddBacklogItemAsync(project.Id, new AddBacklogItemRequest("Story", "Desc", 5, 1), CancellationToken.None);
        var sprint = await service.CreateSprintAsync(
            project.Id,
            new CreateSprintRequest("S1", "Goal", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), [backlog.Id], ["spr001"]),
            CancellationToken.None);

        var updated = await service.UpdateSprintCommitIdsAsync(sprint.Id, new UpdateSprintCommitIdsRequest(["spr001", "spr002"]), CancellationToken.None);

        Assert.Equal(2, updated.CommitIds.Count);
        Assert.Contains("spr001", updated.CommitIds);
        Assert.Contains("spr002", updated.CommitIds);
    }
}

