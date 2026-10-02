using System.Collections.Concurrent;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;

namespace TaskManager.IntegrationTests.Api;

/// <summary>Fakes that honour the same contract as the SQL repositories (ownership scoping, uniqueness).</summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.ContainsKey(id));

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        if (await UsernameExistsAsync(user.Username, cancellationToken))
        {
            throw new ConflictException("username", "Username is already taken.");
        }

        _users[user.Id] = user;
    }
}

internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    public Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, TaskItemStatus? status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TaskItem>>(
        [
            .. _tasks.Values
                .Where(t => t.UserId == userId && (status is null || t.Status == status))
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.CreatedAtUtc),
        ]);

    public Task<TaskItem?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult(_tasks.TryGetValue(taskId, out var task) && task.UserId == userId ? task : null);

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        if (!_tasks.TryGetValue(task.Id, out var existing) || existing.UserId != task.UserId)
        {
            return Task.FromResult(false);
        }

        _tasks[task.Id] = task;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult(_tasks.TryGetValue(taskId, out var task) && task.UserId == userId && _tasks.TryRemove(taskId, out _));
}
