using Microsoft.Extensions.Logging;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// Seeds two demo accounts so reviewers can log in immediately and verify that
/// each user only sees their own tasks. Idempotent: skips users that already exist.
/// </summary>
public sealed partial class DemoDataSeeder(
    IUserRepository userRepository,
    ITaskRepository taskRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    public static readonly (string Username, string Email, string Password)[] DemoAccounts =
    [
        ("demo", "demo@ballastlane.dev", "Demo123!"),
        ("jane", "jane@ballastlane.dev", "Jane123!"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        foreach (var (username, email, password) in DemoAccounts)
        {
            if (await userRepository.UsernameExistsAsync(username, cancellationToken))
            {
                continue;
            }

            var user = User.Create(username, email, passwordHasher.Hash(password), now);
            await userRepository.AddAsync(user, cancellationToken);

            foreach (var task in BuildTasks(user, username, today, now))
            {
                await taskRepository.AddAsync(task, cancellationToken);
            }

            LogSeeded(logger, username);
        }
    }

    private static IEnumerable<TaskItem> BuildTasks(User user, string username, DateOnly today, DateTime now)
    {
        if (username == "jane")
        {
            yield return TaskItem.Create(user.Id, "Review Jane's pull request", "Only Jane can see this task.", today.AddDays(2), now);
            yield break;
        }

        yield return TaskItem.Create(user.Id, "Prepare technical presentation",
            "User story, architecture, TDD approach and GenAI usage.", today.AddDays(1), now);

        var inProgress = TaskItem.Create(user.Id, "Write repository integration tests",
            "Run against a disposable SQL Server database.", today.AddDays(3), now);
        inProgress.Update(inProgress.Title, inProgress.Description, TaskItemStatus.InProgress, inProgress.DueDate, now);
        yield return inProgress;

        var completed = TaskItem.Create(user.Id, "Design the database schema", "Users and Tasks tables.", today, now);
        completed.Update(completed.Title, completed.Description, TaskItemStatus.Completed, completed.DueDate, now);
        yield return completed;

        yield return TaskItem.Create(user.Id, "Add pagination to the task list", null, today.AddDays(14), now);

        // Created a week ago and already past due: demonstrates the "overdue" indicator.
        yield return TaskItem.Rehydrate(Guid.NewGuid(), user.Id, "Renew SSL certificate",
            "This one is overdue on purpose.", TaskItemStatus.Pending, today.AddDays(-2), now.AddDays(-7), now.AddDays(-7));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo user {Username}")]
    private static partial void LogSeeded(ILogger logger, string username);
}
