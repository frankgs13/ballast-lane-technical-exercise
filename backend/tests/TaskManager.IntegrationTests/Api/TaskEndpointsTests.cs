using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;
using static TaskManager.IntegrationTests.Api.TaskManagerApiFactory;

namespace TaskManager.IntegrationTests.Api;

public sealed class TaskEndpointsTests(TaskManagerApiFactory factory) : IClassFixture<TaskManagerApiFactory>
{
    private static readonly DateOnly Tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    [Theory]
    [InlineData("GET", "/api/tasks")]
    [InlineData("GET", "/api/tasks/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("POST", "/api/tasks")]
    [InlineData("PUT", "/api/tasks/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("DELETE", "/api/tasks/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public async Task AllTaskEndpoints_WithoutToken_Return401(string method, string url)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidData_Returns201WithLocationAndBody()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest("Write README", "Setup steps", Tomorrow), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskResponse>(Json);
        Assert.Equal("Write README", created!.Title);
        Assert.Equal(TaskItemStatus.Pending, created.Status);
        Assert.EndsWith($"/api/tasks/{created.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_SerializesStatusAsStringAndDueDateAsIsoDate()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("Title", null, Tomorrow), Json);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Pending", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(Tomorrow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            json.RootElement.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task Create_WithInvalidData_Returns400ProblemDetailsWithAllErrors()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", new { title = "", description = (string?)null }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("dueDate", out _));
    }

    [Fact]
    public async Task Create_WithMalformedJson_Returns400()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/tasks",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithUnknownStatus_Returns400()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateTaskAsync(client, "Title");

        var response = await client.PutAsync($"/api/tasks/{created.Id}", new StringContent(
            $$"""{"title":"Title","status":"Archived","dueDate":"{{Tomorrow:yyyy-MM-dd}}"}""",
            Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FullCrudFlow_Works()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var created = await CreateTaskAsync(client, "Original");

        var fetched = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{created.Id}", Json);
        Assert.Equal("Original", fetched!.Title);

        var updateResponse = await client.PutAsJsonAsync($"/api/tasks/{created.Id}",
            new UpdateTaskRequest("Updated", "More detail", TaskItemStatus.Completed, Tomorrow), Json);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TaskResponse>(Json);
        Assert.Equal("Updated", updated!.Title);
        Assert.Equal(TaskItemStatus.Completed, updated.Status);

        var list = await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json);
        Assert.Single(list!);

        var deleteResponse = await client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task List_WithStatusFilter_ReturnsOnlyMatchingTasks()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();
        var done = await CreateTaskAsync(client, "Done");
        await CreateTaskAsync(client, "Pending");
        await client.PutAsJsonAsync($"/api/tasks/{done.Id}",
            new UpdateTaskRequest("Done", null, TaskItemStatus.Completed, Tomorrow), Json);

        var completed = await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks?status=Completed", Json);

        Assert.Equal("Done", Assert.Single(completed!).Title);
    }

    [Fact]
    public async Task UsersCannotSeeOrModifyEachOthersTasks()
    {
        var (alice, _) = await factory.CreateAuthenticatedClientAsync();
        var (bob, _) = await factory.CreateAuthenticatedClientAsync();
        var aliceTask = await CreateTaskAsync(alice, "Alice's secret");

        var bobList = await bob.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json);
        var bobGet = await bob.GetAsync($"/api/tasks/{aliceTask.Id}");
        var bobUpdate = await bob.PutAsJsonAsync($"/api/tasks/{aliceTask.Id}",
            new UpdateTaskRequest("Hacked", null, TaskItemStatus.Completed, Tomorrow), Json);
        var bobDelete = await bob.DeleteAsync($"/api/tasks/{aliceTask.Id}");

        Assert.Empty(bobList!);
        // 404 rather than 403: do not even confirm that the id exists.
        Assert.Equal(HttpStatusCode.NotFound, bobGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bobUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bobDelete.StatusCode);

        var stillThere = await alice.GetFromJsonAsync<TaskResponse>($"/api/tasks/{aliceTask.Id}", Json);
        Assert.Equal("Alice's secret", stillThere!.Title);
    }

    [Fact]
    public async Task Get_WithNonGuidId_Returns404()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/tasks/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<TaskResponse> CreateTaskAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(title, null, Tomorrow), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }
}
