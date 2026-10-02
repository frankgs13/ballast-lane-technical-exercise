using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// Creates the database (if missing) and applies the embedded, idempotent SQL scripts in name order.
/// A lightweight stand-in for a migrations tool, since EF Core is out of scope for this exercise.
/// </summary>
public sealed partial class DatabaseInitializer(IOptions<DatabaseOptions> options, ILogger<DatabaseInitializer> logger)
{
    private const string ScriptPrefix = "TaskManager.Infrastructure.Persistence.Scripts.";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(options.Value.ConnectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("The connection string must specify a database (Initial Catalog).");
        }

        await EnsureDatabaseExistsAsync(builder, database, cancellationToken);
        await ApplyScriptsAsync(cancellationToken);
    }

    private async Task EnsureDatabaseExistsAsync(
        SqlConnectionStringBuilder builder, string database, CancellationToken cancellationToken)
    {
        var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // Identifiers cannot be parameterized; QUOTENAME makes the dynamic DDL injection-safe.
        command.CommandText = """
            IF DB_ID(@name) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name) + N';';
                EXEC (@sql);
                SELECT 1;
            END
            ELSE
                SELECT 0;
            """;
        command.WithParameter("@name", System.Data.SqlDbType.NVarChar, database, 128);

        var created = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture) == 1;
        if (created)
        {
            LogDatabaseCreated(logger, database);
        }
    }

    private async Task ApplyScriptsAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ScriptPrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        await using var connection = new SqlConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var script in scripts)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = await ReadResourceAsync(assembly, script, cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
            LogScriptApplied(logger, script[ScriptPrefix.Length..]);
        }
    }

    private static async Task<string> ReadResourceAsync(Assembly assembly, string name, CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded script '{name}' not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created database {Database}")]
    private static partial void LogDatabaseCreated(ILogger logger, string database);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied schema script {Script}")]
    private static partial void LogScriptApplied(ILogger logger, string script);
}
