using System.Data;
using Microsoft.Data.SqlClient;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Users;

namespace TaskManager.Infrastructure.Persistence;

internal sealed class UserRepository(ISqlConnectionFactory connectionFactory) : IUserRepository
{
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    private const string SelectColumns = "Id, Username, Email, PasswordHash, CreatedAtUtc";

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        QuerySingleAsync(
            $"SELECT {SelectColumns} FROM dbo.Users WHERE Id = @Id;",
            cmd => cmd.WithParameter("@Id", SqlDbType.UniqueIdentifier, id),
            cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        QuerySingleAsync(
            $"SELECT {SelectColumns} FROM dbo.Users WHERE Username = @Username;",
            cmd => cmd.WithParameter("@Username", SqlDbType.NVarChar, username, User.UsernameMaxLength),
            cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        ExistsAsync(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @Value) THEN 1 ELSE 0 END;",
            SqlDbType.UniqueIdentifier, id, 0, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        ExistsAsync(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Value) THEN 1 ELSE 0 END;",
            SqlDbType.NVarChar, username, User.UsernameMaxLength, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        ExistsAsync(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Value) THEN 1 ELSE 0 END;",
            SqlDbType.NVarChar, email, User.EmailMaxLength, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Users (Id, Username, Email, PasswordHash, CreatedAtUtc)
            VALUES (@Id, @Username, @Email, @PasswordHash, @CreatedAtUtc);
            """;
        command
            .WithParameter("@Id", SqlDbType.UniqueIdentifier, user.Id)
            .WithParameter("@Username", SqlDbType.NVarChar, user.Username, User.UsernameMaxLength)
            .WithParameter("@Email", SqlDbType.NVarChar, user.Email, User.EmailMaxLength)
            .WithParameter("@PasswordHash", SqlDbType.NVarChar, user.PasswordHash, 512)
            .WithParameter("@CreatedAtUtc", SqlDbType.DateTime2, user.CreatedAtUtc);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is UniqueConstraintViolation or UniqueIndexViolation)
        {
            // The service checks uniqueness first, but two concurrent registrations can still race;
            // the database constraint is the real guarantee.
            throw ex.Message.Contains("UQ_Users_Email", StringComparison.Ordinal)
                ? new ConflictException("email", "Email is already registered.")
                : new ConflictException("username", "Username is already taken.");
        }
    }

    private async Task<User?> QuerySingleAsync(
        string sql, Action<SqlCommand> addParameters, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        addParameters(command);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return User.Rehydrate(
            id: reader.GetGuid(0),
            username: reader.GetString(1),
            email: reader.GetString(2),
            passwordHash: reader.GetString(3),
            createdAtUtc: reader.GetUtcDateTime(4));
    }

    private async Task<bool> ExistsAsync(
        string sql, SqlDbType type, object value, int size, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.WithParameter("@Value", type, value, size);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }
}
