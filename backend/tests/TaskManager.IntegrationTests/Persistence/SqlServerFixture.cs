using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.IntegrationTests.Persistence;

/// <summary>
/// Creates a throw-away database per test run so data-access tests exercise the real SQL
/// (constraints, collation, type mapping) and never touch the development database.
/// Override the server with the TASKMANAGER_TEST_SQL environment variable.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DefaultServer =
        @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=10";

    public SqlServerFixture()
    {
        var builder = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("TASKMANAGER_TEST_SQL") ?? DefaultServer)
        {
            InitialCatalog = $"TaskManager_Tests_{Guid.NewGuid():N}",
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public string? UnavailableReason { get; private set; }

    public DatabaseOptions Options => new() { ConnectionString = ConnectionString };

    internal SqlConnectionFactory CreateConnectionFactory() => new(Microsoft.Extensions.Options.Options.Create(Options));

    public async Task InitializeAsync()
    {
        try
        {
            var initializer = new DatabaseInitializer(
                Microsoft.Extensions.Options.Options.Create(Options), NullLogger<DatabaseInitializer>.Instance);
            await initializer.InitializeAsync(CancellationToken.None);
        }
        catch (SqlException ex)
        {
            UnavailableReason = $"SQL Server not reachable ({ex.Message}). Set TASKMANAGER_TEST_SQL to run these tests.";
        }
    }

    public async Task DisposeAsync()
    {
        if (UnavailableReason is not null)
        {
            return;
        }

        SqlConnection.ClearAllPools();
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @sql nvarchar(max) =
                N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ' +
                N'DROP DATABASE ' + QUOTENAME(@name) + N';';
            EXEC (@sql);
            """;
        command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = database;
        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Tasks; DELETE FROM dbo.Users;";
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerGroup : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
