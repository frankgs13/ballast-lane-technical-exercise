namespace TaskManager.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "TaskManager";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Create the database and schema if missing. Convenient for demos; use migrations in production.</summary>
    public bool InitializeOnStartup { get; set; }

    public bool SeedDemoData { get; set; }
}
