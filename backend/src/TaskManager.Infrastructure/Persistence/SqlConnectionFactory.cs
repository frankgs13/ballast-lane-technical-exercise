using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace TaskManager.Infrastructure.Persistence;

internal interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Hands out short-lived connections; ADO.NET connection pooling makes open/close per operation cheap.
/// </summary>
internal sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : ISqlConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
