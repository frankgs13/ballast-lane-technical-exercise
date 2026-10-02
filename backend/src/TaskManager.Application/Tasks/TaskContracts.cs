using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

// Request properties are nullable on purpose: a missing JSON field must surface as a
// validation error ("Status is required") instead of silently defaulting to Pending / 0001-01-01.

public sealed record CreateTaskRequest(string? Title, string? Description, DateOnly? DueDate);

public sealed record UpdateTaskRequest(string? Title, string? Description, TaskItemStatus? Status, DateOnly? DueDate);

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    DateOnly DueDate,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
