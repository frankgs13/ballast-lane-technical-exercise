using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Abstractions;

/// <summary>
/// Every query is scoped by <c>userId</c>: ownership is enforced at the data level,
/// so a caller can never read or modify another user's task by guessing its id.
/// </summary>
public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, TaskItemStatus? status, CancellationToken cancellationToken);

    Task<TaskItem?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    /// <returns><c>false</c> when the task no longer exists.</returns>
    Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken);

    /// <returns><c>false</c> when the task does not exist for this user.</returns>
    Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
}
