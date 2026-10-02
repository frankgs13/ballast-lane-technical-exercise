using Microsoft.Extensions.Time.Testing;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.UnitTests.Application;

public class TaskServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _users.ExistsAsync(UserId, Arg.Any<CancellationToken>()).Returns(true);
        _sut = new TaskService(_tasks, _users, _clock);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheCallersTasks_MappedToResponses()
    {
        var task = NewTask("Mine");
        _tasks.ListByUserAsync(UserId, null, Arg.Any<CancellationToken>()).Returns([task]);

        var result = await _sut.ListAsync(UserId, null, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(task.Id, item.Id);
        Assert.Equal("Mine", item.Title);
        Assert.Equal(TaskItemStatus.Pending, item.Status);
    }

    [Fact]
    public async Task ListAsync_PassesStatusFilterToRepository()
    {
        _tasks.ListByUserAsync(UserId, TaskItemStatus.Completed, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ListAsync(UserId, TaskItemStatus.Completed, CancellationToken.None);

        await _tasks.Received(1).ListByUserAsync(UserId, TaskItemStatus.Completed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_FlagsOverdueTasks()
    {
        var overdue = TaskItem.Rehydrate(Guid.NewGuid(), UserId, "Late", null, TaskItemStatus.Pending,
            Today.AddDays(-3), Now.UtcDateTime.AddDays(-10), Now.UtcDateTime.AddDays(-10));
        _tasks.ListByUserAsync(UserId, null, Arg.Any<CancellationToken>()).Returns([overdue]);

        var result = await _sut.ListAsync(UserId, null, CancellationToken.None);

        Assert.True(result[0].IsOverdue);
    }

    [Fact]
    public async Task GetAsync_WhenTaskExists_ReturnsIt()
    {
        var task = NewTask("Mine");
        _tasks.GetByIdAsync(UserId, task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await _sut.GetAsync(UserId, task.Id, CancellationToken.None);

        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public async Task GetAsync_WhenTaskDoesNotExistOrBelongsToAnotherUser_ThrowsNotFound()
    {
        _tasks.GetByIdAsync(UserId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetAsync(UserId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_PersistsTaskOwnedByCaller()
    {
        var request = new CreateTaskRequest("Prepare demo", "Seed data", Today.AddDays(2));

        var result = await _sut.CreateAsync(UserId, request, CancellationToken.None);

        await _tasks.Received(1).AddAsync(
            Arg.Is<TaskItem>(t => t.UserId == UserId && t.Title == "Prepare demo" && t.Status == TaskItemStatus.Pending),
            Arg.Any<CancellationToken>());
        Assert.Equal("Prepare demo", result.Title);
        Assert.Equal(Now.UtcDateTime, result.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ThrowsAndDoesNotPersist()
    {
        var request = new CreateTaskRequest("", null, Today);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.CreateAsync(UserId, request, CancellationToken.None));

        await _tasks.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_WhenUserNoLongerExists_ThrowsNotFound()
    {
        var ghost = Guid.NewGuid();
        _users.ExistsAsync(ghost, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.CreateAsync(ghost, new CreateTaskRequest("Title", null, Today), CancellationToken.None));

        await _tasks.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesAndPersists()
    {
        var task = NewTask("Old");
        _tasks.GetByIdAsync(UserId, task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _tasks.UpdateAsync(task, Arg.Any<CancellationToken>()).Returns(true);
        _clock.Advance(TimeSpan.FromHours(1));

        var result = await _sut.UpdateAsync(UserId, task.Id,
            new UpdateTaskRequest("New", "Details", TaskItemStatus.Completed, task.DueDate), CancellationToken.None);

        Assert.Equal("New", result.Title);
        Assert.Equal(TaskItemStatus.Completed, result.Status);
        Assert.Equal(Now.UtcDateTime.AddHours(1), result.UpdatedAtUtc);
        await _tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenTaskNotFound_ThrowsNotFound()
    {
        _tasks.GetByIdAsync(UserId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateAsync(UserId, Guid.NewGuid(),
            new UpdateTaskRequest("New", null, TaskItemStatus.Pending, Today), CancellationToken.None));

        await _tasks.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_WhenTaskDeletedConcurrently_ThrowsNotFound()
    {
        var task = NewTask("Old");
        _tasks.GetByIdAsync(UserId, task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _tasks.UpdateAsync(task, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateAsync(UserId, task.Id,
            new UpdateTaskRequest("New", null, TaskItemStatus.Pending, task.DueDate), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidRequest_ThrowsAndDoesNotPersist()
    {
        var task = NewTask("Old");
        _tasks.GetByIdAsync(UserId, task.Id, Arg.Any<CancellationToken>()).Returns(task);

        await Assert.ThrowsAsync<DomainValidationException>(() => _sut.UpdateAsync(UserId, task.Id,
            new UpdateTaskRequest(" ", null, null, task.DueDate), CancellationToken.None));

        await _tasks.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_WhenTaskExists_Deletes()
    {
        var taskId = Guid.NewGuid();
        _tasks.DeleteAsync(UserId, taskId, Arg.Any<CancellationToken>()).Returns(true);

        await _sut.DeleteAsync(UserId, taskId, CancellationToken.None);

        await _tasks.Received(1).DeleteAsync(UserId, taskId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenTaskNotFound_ThrowsNotFound()
    {
        _tasks.DeleteAsync(UserId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.DeleteAsync(UserId, Guid.NewGuid(), CancellationToken.None));
    }

    private static TaskItem NewTask(string title) =>
        TaskItem.Create(UserId, title, null, Today.AddDays(1), Now.UtcDateTime);
}
