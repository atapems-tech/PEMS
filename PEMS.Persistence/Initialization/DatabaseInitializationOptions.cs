namespace PEMS.Persistence.Initialization;

/// <summary>
/// Code-first database bootstrap settings (configuration section "Database").
/// Defaults make a brand-new, empty SQL Server instance start with a complete schema and no manual SQL.
/// </summary>
public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "Database";

    /// <summary>Create the database named in the connection string when it does not exist (needs CREATE DATABASE rights; skipped with a warning otherwise).</summary>
    public bool CreateIfMissing { get; set; } = true;

    /// <summary>Apply every pending EF Core migration before the application starts serving requests.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Create the full-text catalog and indexes (idempotent; skipped when Full-Text Search is not installed).</summary>
    public bool ApplyFullTextSearch { get; set; } = true;

    /// <summary>Command timeout used while migrating; the first run executes the whole schema.</summary>
    public int CommandTimeoutSeconds { get; set; } = 600;
}
