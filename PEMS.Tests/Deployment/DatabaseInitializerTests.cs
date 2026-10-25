using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using PEMS.Application.DTOs;
using PEMS.Persistence.Context;
using PEMS.Persistence.Initialization;

namespace PEMS.Tests.Deployment;

/// <summary>
/// Code-first database: pure rules (install vs update classification, options defaults) and the shape of
/// the migration chain. The real-SQL behaviour lives in <see cref="DatabaseUpdateServiceSqlTests"/>.
/// </summary>
public class DatabaseInitializerTests
{
    private static readonly string[] Known = { "M1", "M2", "M3" };

    [Fact]
    public void Empty_database_without_tables_is_a_first_run()
    {
        Assert.Equal(DatabaseSetupState.FirstRun,
            DatabaseSetupClassifier.Classify(hasUserTables: false, Array.Empty<string>(), Known));
    }

    [Fact]
    public void Tables_without_migration_history_are_never_treated_as_an_install()
    {
        Assert.Equal(DatabaseSetupState.UnmanagedSchema,
            DatabaseSetupClassifier.Classify(hasUserTables: true, Array.Empty<string>(), Known));
    }

    [Fact]
    public void Existing_database_with_missing_migrations_is_an_update()
    {
        Assert.Equal(DatabaseSetupState.UpdateAvailable,
            DatabaseSetupClassifier.Classify(true, new[] { "M1" }, Known));
    }

    [Fact]
    public void All_migrations_applied_is_up_to_date()
    {
        Assert.Equal(DatabaseSetupState.UpToDate,
            DatabaseSetupClassifier.Classify(true, Known, Known));
    }

    [Fact]
    public void Database_with_unknown_migrations_is_newer_than_the_application()
    {
        Assert.Equal(DatabaseSetupState.DatabaseNewerThanApplication,
            DatabaseSetupClassifier.Classify(true, new[] { "M1", "M99" }, Known));
    }

    [Fact]
    public void Options_default_to_a_fully_automatic_bootstrap()
    {
        var options = new DatabaseInitializationOptions();

        Assert.True(options.CreateIfMissing);
        Assert.True(options.MigrateOnStartup);
    }

    [Fact]
    public void Migration_chain_is_ordered_unique_and_contains_the_code_first_additions_in_order()
    {
        var ids = typeof(PEMSDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => id != null)
            .Cast<string>()
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());

        var ordered = ids.OrderBy(i => i, StringComparer.Ordinal).ToList();
        // Newer migrations may follow (the chain grows with every release); the code-first additions must exist
        // and keep their relative order: DatabaseUpdateRuns first, then FullTextSearch.
        var updateRuns = ordered.FindIndex(i => i.EndsWith("_DatabaseUpdateRuns", StringComparison.Ordinal));
        var fullText = ordered.FindIndex(i => i.EndsWith("_FullTextSearch", StringComparison.Ordinal));
        Assert.True(updateRuns >= 0, "migration *_DatabaseUpdateRuns is missing");
        Assert.True(fullText >= 0, "migration *_FullTextSearch is missing");
        Assert.True(updateRuns < fullText, "DatabaseUpdateRuns must precede FullTextSearch");
    }
}
