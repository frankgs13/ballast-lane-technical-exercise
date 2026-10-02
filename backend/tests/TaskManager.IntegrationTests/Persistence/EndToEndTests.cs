using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Application.Auth;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;
using TaskManager.IntegrationTests.Api;
using static TaskManager.IntegrationTests.Api.TaskManagerApiFactory;

namespace TaskManager.IntegrationTests.Persistence;

/// <summary>Full stack: HTTP -> controllers -> services -> ADO.NET -> SQL Server.</summary>
[Collection(SqlServerGroup.Name)]
public sealed class EndToEndTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly SqlBackedApiFactory _factory = new(fixture.ConnectionString);

    public async Task InitializeAsync()
    {
        if (fixture.UnavailableReason is null)
        {
            await fixture.ResetAsync();
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [SkippableFact]
    public async Task RegisterLoginAndManageTasks_AgainstRealDatabase()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var client = _factory.CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("e2e.user", "e2e@example.com", "Sup3rSecret!"), Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("E2E.USER", "Sup3rSecret!"), Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var create = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("From E2E", "Stored in SQL", tomorrow), Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<TaskResponse>(Json))!;

        var update = await client.PutAsJsonAsync($"/api/tasks/{created.Id}",
            new UpdateTaskRequest("From E2E (edited)", null, TaskItemStatus.InProgress, tomorrow.AddDays(1)), Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json);
        var stored = Assert.Single(list!);
        Assert.Equal("From E2E (edited)", stored.Title);
        Assert.Null(stored.Description);
        Assert.Equal(TaskItemStatus.InProgress, stored.Status);

        var delete = await client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json))!);
    }

    private sealed class SqlBackedApiFactory(string connectionString) : TaskManagerApiFactory
    {
        protected override string? ConnectionString => connectionString;
    }
}
