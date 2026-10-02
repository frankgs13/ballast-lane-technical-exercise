using System.Data;
using Microsoft.Data.SqlClient;

namespace TaskManager.Infrastructure.Persistence;

internal static class SqlCommandExtensions
{
    /// <summary>
    /// Always declare type and size explicitly. <c>AddWithValue</c> infers NVARCHAR(len) per call, which
    /// fragments the plan cache and can cause implicit conversions that defeat indexes.
    /// </summary>
    public static SqlCommand WithParameter(this SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = command.Parameters.Add(name, type);
        if (size > 0)
        {
            parameter.Size = size;
        }

        parameter.Value = value ?? DBNull.Value;
        return command;
    }

    public static DateTime GetUtcDateTime(this SqlDataReader reader, int ordinal) =>
        DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);

    public static DateOnly GetDateOnly(this SqlDataReader reader, int ordinal) =>
        DateOnly.FromDateTime(reader.GetDateTime(ordinal));

    public static string? GetNullableString(this SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
