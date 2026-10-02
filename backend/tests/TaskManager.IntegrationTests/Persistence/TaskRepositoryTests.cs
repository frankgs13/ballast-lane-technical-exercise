using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class TaskRepositoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime NowUtc = new(2026, 10, 2, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(NowUtc);

    private readonly TaskRepository _sut = new(fixture.CreateConnectionFactory());
    private readonly UserRepository _users = new(fixture.CreateConnectionFactory());
    private User _owner = null!;
    private User _otherUser = null!;

    public async Task InitializeAsync()
    {
        if (fixture.UnavailableReason is not null)
        {
            return;
        }

        await fixture.ResetAsync();
        _owner = User.Create("owner", "owner@example.com", "hash", NowUtc);
        _otherUser = User.Create("other", "other@example.com", "hash", NowUtc);
        await _users.AddAsync(_owner, CancellationToken.None);
        await _users.AddAsync(_otherUser, CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableFact]
    public async Task AddAsync_ThenGetById_RoundTripsAllFields()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var task = TaskItem.Create(_owner.Id, "Write tests", "With real SQL", Today.AddDays(3), NowUtc);

        await _sut.AddAsync(task, CancellationToken.None);
        var loaded = await _sut.GetByIdAsync(_owner.Id, task.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(task.Title, loaded.Title);
        Assert.Equal(task.Description, loaded.Description);
        Assert.Equal(task.Status, loaded.Status);
        Assert.Equal(task.DueDate, loaded.DueDate);
        Assert.Equal(NowUtc, loaded.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.UpdatedAtUtc.Kind);
    }

    [SkippableFact]
    public async Task AddAsync_WithNullDescription_RoundTripsNull()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var task = TaskItem.Create(_owner.Id, "No description", null, Today, NowUtc);

        await _sut.AddAsync(task, CancellationToken.None);
        var loaded = await _sut.GetByIdAsync(_owner.Id, task.Id, CancellationToken.None);

        Assert.Null(loaded!.Description);
    }

    [SkippableFact]
    public async Task GetByIdAsync_ForAnotherUsersTask_ReturnsNull()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var task = TaskItem.Create(_owner.Id, "Private", null, Today, NowUtc);
        await _sut.AddAsync(task, CancellationToken.None);

        Assert.Null(await _sut.GetByIdAsync(_otherUser.Id, task.Id, CancellationToken.None));
    }

    [SkippableFact]
    public async Task ListByUserAsync_ReturnsOnlyOwnTasks_OrderedByDueDate()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        await _sut.AddAsync(TaskItem.Create(_owner.Id, "Later", null, Today.AddDays(10), NowUtc), CancellationToken.None);
        await _sut.AddAsync(TaskItem.Create(_owner.Id, "Sooner", null, Today.AddDays(1), NowUtc), CancellationToken.None);
        await _sut.AddAsync(TaskItem.Create(_otherUser.Id, "Not mine", null, Today, NowUtc), CancellationToken.None);

        var result = await _sut.ListByUserAsync(_owner.Id, null, CancellationToken.None);

        Assert.Equal(["Sooner", "Later"], result.Select(t => t.Title));
    }

    [SkippableFact]
    public async Task ListByUserAsync_WithStatusFilter_ReturnsMatchingTasksOnly()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var done = TaskItem.Create(_owner.Id, "Done", null, Today, NowUtc);
        done.Update("Done", null, TaskItemStatus.Completed, Today, NowUtc);
        await _sut.AddAsync(done, CancellationToken.None);
        await _sut.AddAsync(TaskItem.Create(_owner.Id, "Pending", null, Today, NowUtc), CancellationToken.None);

        var result = await _sut.ListByUserAsync(_owner.Id, TaskItemStatus.Completed, CancellationToken.None);

        Assert.Equal("Done", Assert.Single(result).Title);
    }

    [SkippableFact]
    public async Task UpdateAsync_PersistsChanges()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var task = TaskItem.Create(_owner.Id, "Original", null, Today, NowUtc);
        await _sut.AddAsync(task, CancellationToken.None);

        task.Update("Changed", "Now with description", TaskItemStatus.InProgress, Today.AddDays(2), NowUtc.AddHours(2));
        var updated = await _sut.UpdateAsync(task, CancellationToken.None);
        var loaded = await _sut.GetByIdAsync(_owner.Id, task.Id, CancellationToken.None);

        Assert.True(updated);
        Assert.Equal("Changed", loaded!.Title);
        Assert.Equal("Now with description", loaded.Description);
        Assert.Equal(TaskItemStatus.InProgress, loaded.Status);
        Assert.Equal(Today.AddDays(2), loaded.DueDate);
        Assert.Equal(NowUtc.AddHours(2), loaded.UpdatedAtUtc);
        Assert.Equal(NowUtc, loaded.CreatedAtUtc);
    }

    [SkippableFact]
    public async Task UpdateAsync_WhenTaskDoesNotExist_ReturnsFalse()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var neverSaved = TaskItem.Create(_owner.Id, "Ghost", null, Today, NowUtc);

        Assert.False(await _sut.UpdateAsync(neverSaved, CancellationToken.None));
    }

    [SkippableFact]
    public async Task DeleteAsync_RemovesTask()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var task = TaskItem.Create(_owner.Id, "To delete", null, Today, NowUtc);
        await _sut.AddAsync(task, CancellationToken.None);

        Assert.True(await _sut.DeleteAsync(_owner.Id, task.Id, CancellationToken.None));
        Assert.Null(await _sut.GetByIdAsync(_owner.Id, task.Id, CancellationToken.None));
    }

    [SkippableFact]
    public async Task DeleteAsync_ForAnotherUsersTask_ReturnsFalseAndKeepsIt()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        var task = TaskItem.Create(_owner.Id, "Private", null, Today, NowUtc);
        await _sut.AddAsync(task, CancellationToken.None);

        Assert.False(await _sut.DeleteAsync(_otherUser.Id, task.Id, CancellationToken.None));
        Assert.NotNull(await _sut.GetByIdAsync(_owner.Id, task.Id, CancellationToken.None));
    }

    [SkippableFact]
    public async Task AddAsync_WithUnicodeAndQuotes_StoresTextVerbatim()
    {
        Skip.If(fixture.UnavailableReason is not null, fixture.UnavailableReason);
        const string title = "Revisión O'Brien; DROP TABLE dbo.Tasks; -- ñ 🚀";
        var task = TaskItem.Create(_owner.Id, title, null, Today, NowUtc);

        await _sut.AddAsync(task, CancellationToken.None);
        var loaded = await _sut.GetByIdAsync(_owner.Id, task.Id, CancellationToken.None);

        Assert.Equal(title, loaded!.Title);
    }
}
