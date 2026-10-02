using System.Data;
using Microsoft.Data.SqlClient;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Tasks;

namespace TaskManager.Infrastructure.Persistence;

internal sealed class TaskRepository(ISqlConnectionFactory connectionFactory) : ITaskRepository
{
    private const string SelectColumns =
        "Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc, UpdatedAtUtc";

    public async Task<IReadOnlyList<TaskItem>> ListByUserAsync(
        Guid userId, TaskItemStatus? status, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // A single static statement (instead of string-building an optional WHERE) keeps one cached plan
        // and removes any temptation to concatenate input into SQL.
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM dbo.Tasks
            WHERE UserId = @UserId AND (@Status IS NULL OR Status = @Status)
            ORDER BY DueDate, CreatedAtUtc;
            """;
        command
            .WithParameter("@UserId", SqlDbType.UniqueIdentifier, userId)
            .WithParameter("@Status", SqlDbType.TinyInt, status is null ? null : (byte)status.Value);

        var tasks = new List<TaskItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(Map(reader));
        }

        return tasks;
    }

    public async Task<TaskItem?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM dbo.Tasks WHERE Id = @Id AND UserId = @UserId;";
        command
            .WithParameter("@Id", SqlDbType.UniqueIdentifier, taskId)
            .WithParameter("@UserId", SqlDbType.UniqueIdentifier, userId);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Tasks (Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@Id, @UserId, @Title, @Description, @Status, @DueDate, @CreatedAtUtc, @UpdatedAtUtc);
            """;
        AddTaskParameters(command, task)
            .WithParameter("@CreatedAtUtc", SqlDbType.DateTime2, task.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Tasks
            SET Title = @Title,
                Description = @Description,
                Status = @Status,
                DueDate = @DueDate,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE Id = @Id AND UserId = @UserId;
            """;
        AddTaskParameters(command, task);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Tasks WHERE Id = @Id AND UserId = @UserId;";
        command
            .WithParameter("@Id", SqlDbType.UniqueIdentifier, taskId)
            .WithParameter("@UserId", SqlDbType.UniqueIdentifier, userId);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static SqlCommand AddTaskParameters(SqlCommand command, TaskItem task) => command
        .WithParameter("@Id", SqlDbType.UniqueIdentifier, task.Id)
        .WithParameter("@UserId", SqlDbType.UniqueIdentifier, task.UserId)
        .WithParameter("@Title", SqlDbType.NVarChar, task.Title, TaskItem.TitleMaxLength)
        .WithParameter("@Description", SqlDbType.NVarChar, task.Description, TaskItem.DescriptionMaxLength)
        .WithParameter("@Status", SqlDbType.TinyInt, (byte)task.Status)
        .WithParameter("@DueDate", SqlDbType.Date, task.DueDate.ToDateTime(TimeOnly.MinValue))
        .WithParameter("@UpdatedAtUtc", SqlDbType.DateTime2, task.UpdatedAtUtc);

    private static TaskItem Map(SqlDataReader reader) => TaskItem.Rehydrate(
        id: reader.GetGuid(0),
        userId: reader.GetGuid(1),
        title: reader.GetString(2),
        description: reader.GetNullableString(3),
        status: (TaskItemStatus)reader.GetByte(4),
        dueDate: reader.GetDateOnly(5),
        createdAtUtc: reader.GetUtcDateTime(6),
        updatedAtUtc: reader.GetUtcDateTime(7));
}
