using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tasks;

/// <summary>
/// A unit of work owned by a single user. Named TaskItem to avoid clashing with System.Threading.Tasks.Task.
/// </summary>
public sealed class TaskItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// "Today" is computed in UTC, but users pick dates in their local time zone. Allowing one day
    /// of slack keeps a user in UTC-12 from being rejected when choosing their local "today".
    /// </summary>
    private const int DueDateToleranceDays = 1;

    private TaskItem(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly dueDate,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Description = description;
        Status = status;
        DueDate = dueDate;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public DateOnly DueDate { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static TaskItem Create(Guid userId, string? title, string? description, DateOnly? dueDate, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A task must belong to a user.", nameof(userId));
        }

        var errors = new ValidationErrors();
        var normalizedTitle = ValidateTitle(title, errors);
        var normalizedDescription = ValidateDescription(description, errors);
        ValidateDueDate(dueDate, nowUtc, errors);
        errors.ThrowIfAny();

        return new TaskItem(
            Guid.NewGuid(),
            userId,
            normalizedTitle!,
            normalizedDescription,
            TaskItemStatus.Pending,
            dueDate!.Value,
            nowUtc,
            nowUtc);
    }

    /// <summary>Rebuilds an entity from persisted state. No validation: the data was valid when stored.</summary>
    public static TaskItem Rehydrate(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly dueDate,
        DateTime createdAtUtc,
        DateTime updatedAtUtc) =>
        new(id, userId, title, description, status, dueDate, createdAtUtc, updatedAtUtc);

    public void Update(string? title, string? description, TaskItemStatus? status, DateOnly? dueDate, DateTime nowUtc)
    {
        var errors = new ValidationErrors();
        var normalizedTitle = ValidateTitle(title, errors);
        var normalizedDescription = ValidateDescription(description, errors);

        if (status is null)
        {
            errors.Add("status", "Status is required.");
        }
        else if (!Enum.IsDefined(status.Value))
        {
            errors.Add("status", $"Status must be one of: {string.Join(", ", Enum.GetNames<TaskItemStatus>())}.");
        }

        // An overdue task must remain editable (e.g. to mark it Completed), so the
        // "not in the past" rule only applies when the due date actually changes.
        if (dueDate != DueDate)
        {
            ValidateDueDate(dueDate, nowUtc, errors);
        }

        errors.ThrowIfAny();

        Title = normalizedTitle!;
        Description = normalizedDescription;
        Status = status!.Value;
        DueDate = dueDate!.Value;
        UpdatedAtUtc = nowUtc;
    }

    public bool IsOverdue(DateOnly today) => Status != TaskItemStatus.Completed && DueDate < today;

    private static string? ValidateTitle(string? title, ValidationErrors errors)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            errors.Add("title", "Title is required.");
        }
        else if (normalized.Length > TitleMaxLength)
        {
            errors.Add("title", $"Title must be {TitleMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? ValidateDescription(string? description, ValidationErrors errors)
    {
        var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalized?.Length > DescriptionMaxLength)
        {
            errors.Add("description", $"Description must be {DescriptionMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static void ValidateDueDate(DateOnly? dueDate, DateTime nowUtc, ValidationErrors errors)
    {
        if (dueDate is null)
        {
            errors.Add("dueDate", "Due date is required.");
            return;
        }

        var earliestAllowed = DateOnly.FromDateTime(nowUtc).AddDays(-DueDateToleranceDays);
        if (dueDate.Value < earliestAllowed)
        {
            errors.Add("dueDate", "Due date cannot be in the past.");
        }
    }
}
