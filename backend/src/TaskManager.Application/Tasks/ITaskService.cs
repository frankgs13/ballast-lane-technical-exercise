using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

public interface ITaskService
{
    Task<IReadOnlyList<TaskResponse>> ListAsync(Guid userId, TaskItemStatus? status, CancellationToken cancellationToken);

    Task<TaskResponse> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken);

    Task<TaskResponse> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
}
