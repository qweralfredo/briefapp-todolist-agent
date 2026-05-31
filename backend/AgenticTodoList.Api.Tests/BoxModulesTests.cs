using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefappTodoList.Api.Contracts;
using BriefappTodoList.Api.Domain;

namespace BriefappTodoList.Api.Tests;

public class BoxModulesTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public BoxModulesTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateTestProject(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(name, "Test Box"));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task UsersModule_ShouldAddListAndRemoveUsers()
    {
        var boxId = await CreateTestProject("UsersBox");

        // Add user
        var addRes = await _client.PostAsJsonAsync($"/api/boxes/{boxId}/users", new
        {
            email = "user@test.com",
            role = "viewer"
        });
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        var addedUser = await addRes.Content.ReadFromJsonAsync<JsonElement>();
        var userId = addedUser.GetProperty("id").GetGuid();

        // Update user
        var updateRes = await _client.PutAsJsonAsync($"/api/boxes/{boxId}/users/{userId}", new
        {
            role = "editor",
            groups = "teamA,teamB"
        });
        updateRes.EnsureSuccessStatusCode();

        // List
        var list = await _client.GetFromJsonAsync<List<JsonElement>>($"/api/boxes/{boxId}/users");
        Assert.NotNull(list);
        var found = list!.First(u => u.GetProperty("id").GetGuid() == userId);
        Assert.Equal("editor", found.GetProperty("role").GetString());

        // Delete
        var delRes = await _client.DeleteAsync($"/api/boxes/{boxId}/users/{userId}");
        delRes.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MemoryModule_ShouldUpsertListAndRemoveMemories()
    {
        var boxId = await CreateTestProject("MemoryBox");

        // Upsert 1
        var putReq1 = new HttpRequestMessage(HttpMethod.Put, $"/api/boxes/{boxId}/memory");
        putReq1.Content = JsonContent.Create(new { key = "key1", value = "val1", tags = "t1" });
        var putRes1 = await _client.SendAsync(putReq1);
        putRes1.EnsureSuccessStatusCode();
        
        var mem1 = await putRes1.Content.ReadFromJsonAsync<JsonElement>();
        var memId = mem1.GetProperty("id").GetGuid();

        // Check list
        var listRes = await _client.GetFromJsonAsync<List<JsonElement>>($"/api/boxes/{boxId}/memory?tags=t1");
        Assert.NotNull(listRes);
        Assert.Single(listRes!);

        // Delete
        var delRes = await _client.DeleteAsync($"/api/boxes/{boxId}/memory/key1");
        delRes.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LogModule_ShouldCreateAndListLogs()
    {
        var boxId = await CreateTestProject("LogBox");

        var addRes = await _client.PostAsJsonAsync($"/api/boxes/{boxId}/logs", new CreateLogRequest("error", "test-agent", "Msg", "Details"));
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);

        var listRes = await _client.GetFromJsonAsync<List<JsonElement>>($"/api/boxes/{boxId}/logs?limit=5");
        Assert.NotNull(listRes);
        Assert.Single(listRes!);
        Assert.Equal("error", listRes![0].GetProperty("level").GetString());
    }

    [Fact]
    public async Task UsageModule_ShouldReturnSummary()
    {
        var boxId = await CreateTestProject("UsageBox");

        // Summary when empty
        var summary = await _client.GetFromJsonAsync<BoxUsageSummaryDto>($"/api/boxes/{boxId}/usage");
        Assert.NotNull(summary);
        Assert.Equal(0, summary!.TotalRuns);

        // Add an agent run
        var runResponse = await _client.PostAsJsonAsync($"/api/projects/{boxId}/agent-runs", new
        {
            agentName = "usa-agent",
            entryPoint = "test",
            inputSummary = "i",
            outputSummary = "o",
            status = "done",
            startedAt = DateTimeOffset.UtcNow,
            modelName = "claude-test",
            tokensInput = 10,
            tokensOutput = 20,
            costUsd = 1.5m,
            success = true
        });
        runResponse.EnsureSuccessStatusCode();

        // Check summary again
        var updatedSummary = await _client.GetFromJsonAsync<BoxUsageSummaryDto>($"/api/boxes/{boxId}/usage");
        Assert.NotNull(updatedSummary);
        Assert.Equal(1, updatedSummary!.TotalRuns);
        Assert.Equal(10, updatedSummary.TotalTokensInput);
        Assert.Equal(20, updatedSummary.TotalTokensOutput);
        Assert.Equal(1.5m, updatedSummary.TotalCostUsd);
        Assert.Equal(100.0, updatedSummary.SuccessRatePct);
        Assert.Contains("claude-test", updatedSummary.RunsByModel.Keys);
    }

    [Fact]
    public async Task ApiKeysModule_ShouldGenerateAndListKeys()
    {
        var boxId = await CreateTestProject("ApiKeysBox");

        // Create key
        var createRes = await _client.PostAsJsonAsync($"/api/boxes/{boxId}/api-keys", new CreateApiKeyRequest("CI Key", "read,write"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        
        var keyRes = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var keyId = keyRes.GetProperty("id").GetGuid();
        var rawKey = keyRes.GetProperty("key").GetString();
        Assert.False(string.IsNullOrEmpty(rawKey));

        // List keys
        var listRes = await _client.GetFromJsonAsync<List<JsonElement>>($"/api/boxes/{boxId}/api-keys");
        Assert.NotNull(listRes);
        Assert.Single(listRes!);
        
        // Assert raw key is NEVER returned in the list endpoint!
        var listItem = listRes![0];
        Assert.False(listItem.TryGetProperty("key", out _));
        Assert.True(listItem.TryGetProperty("prefix", out _));

        // Revoke
        var revokeRes = await _client.DeleteAsync($"/api/boxes/{boxId}/api-keys/{keyId}");
        revokeRes.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AllowListModule_ShouldUpsertListAndToggle()
    {
        var boxId = await CreateTestProject("AllowListBox");

        // Upsert
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/api/boxes/{boxId}/allow-list");
        putReq.Content = JsonContent.Create(new UpsertAllowListRequest("TestApp", "http://callback", "read"));
        var putRes = await _client.SendAsync(putReq);
        putRes.EnsureSuccessStatusCode();

        var entry = await putRes.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = entry.GetProperty("id").GetGuid();

        // Toggle
        var toggleRes = await _client.PatchAsync($"/api/boxes/{boxId}/allow-list/{entryId}/toggle", null);
        toggleRes.EnsureSuccessStatusCode();
        var toggled = await toggleRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(toggled.GetProperty("isActive").GetBoolean());

        // Delete
        var delRes = await _client.DeleteAsync($"/api/boxes/{boxId}/allow-list/{entryId}");
        delRes.EnsureSuccessStatusCode();
    }
}
