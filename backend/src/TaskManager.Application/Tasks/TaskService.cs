using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

internal sealed class TaskService(
    ITaskRepository taskRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider) : ITaskService
{
    private const string Resource = "Task";

    public async Task<IReadOnlyList<TaskResponse>> ListAsync(
        Guid userId, TaskItemStatus? status, CancellationToken cancellationToken)
    {
        var tasks = await taskRepository.ListByUserAsync(userId, status, cancellationToken);
        var today = Today();
        return [.. tasks.Select(t => ToResponse(t, today))];
    }

    public async Task<TaskResponse> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await taskRepository.GetByIdAsync(userId, taskId, cancellationToken)
            ?? throw new NotFoundException(Resource, taskId);

        return ToResponse(task, Today());
    }

    public async Task<TaskResponse> CreateAsync(
        Guid userId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The token can outlive the account it was issued for.
        if (!await userRepository.ExistsAsync(userId, cancellationToken))
        {
            throw new NotFoundException("User", userId);
        }

        var task = TaskItem.Create(userId, request.Title, request.Description, request.DueDate, UtcNow());
        await taskRepository.AddAsync(task, cancellationToken);

        return ToResponse(task, Today());
    }

    public async Task<TaskResponse> UpdateAsync(
        Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var task = await taskRepository.GetByIdAsync(userId, taskId, cancellationToken)
            ?? throw new NotFoundException(Resource, taskId);

        task.Update(request.Title, request.Description, request.Status, request.DueDate, UtcNow());

        if (!await taskRepository.UpdateAsync(task, cancellationToken))
        {
            throw new NotFoundException(Resource, taskId);
        }

        return ToResponse(task, Today());
    }

    public async Task DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        if (!await taskRepository.DeleteAsync(userId, taskId, cancellationToken))
        {
            throw new NotFoundException(Resource, taskId);
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private DateOnly Today() => DateOnly.FromDateTime(UtcNow());

    private static TaskResponse ToResponse(TaskItem task, DateOnly today) => new(
        task.Id,
        task.Title,
        task.Description,
        task.Status,
        task.DueDate,
        task.IsOverdue(today),
        task.CreatedAtUtc,
        task.UpdatedAtUtc);
}
