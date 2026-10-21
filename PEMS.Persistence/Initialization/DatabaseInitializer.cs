using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Initialization;

/// <summary>
/// Makes the database fully code-first: the schema is created and upgraded from the EF Core model/migrations
/// and the embedded full-text script, with no hand-run SQL. Every step is idempotent and safe to repeat.
/// </summary>
public static class DatabaseInitializer
{
    public const string FullTextResourceName = "PEMS.Scripts.EnableFullTextSearch.sql";

    private static readonly Regex GoSeparator = new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex UseStatement = new(@"^\s*USE\s+\[?\w+\]?\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Creates the target database when it is missing. Must run before anything else opens the connection
    /// (for example Hangfire's storage, which installs its own schema on configuration).
    /// A failure is logged and ignored: the database may already exist and the login may lack CREATE DATABASE.
    /// </summary>
    public static async Task EnsureDatabaseExistsAsync(
        string? connectionString, DatabaseInitializationOptions options, ILogger logger, CancellationToken ct = default)
    {
        if (!options.CreateIfMissing || string.IsNullOrWhiteSpace(connectionString))
            return;

        try
        {
            var target = new SqlConnectionStringBuilder(connectionString);
            var database = target.InitialCatalog;
            if (string.IsNullOrWhiteSpace(database))
                return;

            target.InitialCatalog = "master";
            target.ConnectTimeout = Math.Max(target.ConnectTimeout, 15);

            await using var connection = new SqlConnection(target.ConnectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "IF DB_ID(@name) IS NULL BEGIN DECLARE @sql nvarchar(400) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC (@sql); SELECT 1; END ELSE SELECT 0;";
            command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = database });
            var created = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
            if (created)
                logger.LogInformation("Database '{Database}' did not exist and was created.", database);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check/create the database before startup; continuing (it may already exist or the login lacks CREATE DATABASE).");
        }
    }

    /// <summary>Applies pending migrations, then the full-text search objects.</summary>
    public static async Task MigrateAsync(
        IServiceProvider services, DatabaseInitializationOptions options, ILogger logger, CancellationToken ct = default)
    {
        if (!options.MigrateOnStartup)
        {
            logger.LogInformation("Database:MigrateOnStartup is off; the schema is expected to be managed externally.");
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(options.CommandTimeoutSeconds));

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending database migration(s); latest: {Latest}.", pending.Count, pending[^1]);
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database schema is up to date.");
        }
        else
        {
            logger.LogInformation("Database schema is already up to date.");
        }

        if (options.ApplyFullTextSearch)
            await ApplyFullTextSearchAsync(db, logger, ct);
    }

    private static async Task ApplyFullTextSearchAsync(PEMSDbContext db, ILogger logger, CancellationToken ct)
    {
        try
        {
            var batches = SplitBatches(ReadEmbeddedScript(FullTextResourceName));
            var connection = db.Database.GetDbConnection();
            var wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed)
                await connection.OpenAsync(ct);
            try
            {
                foreach (var batch in batches)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = batch;
                    command.CommandTimeout = 300;
                    await command.ExecuteNonQueryAsync(ct);
                }
            }
            finally
            {
                if (wasClosed)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            // Search falls back to LIKE queries, so this must never block startup.
            logger.LogWarning(ex, "Full-text search setup was skipped.");
        }
    }

    /// <summary>Removes USE statements (the script targets the current database) and splits on GO lines.</summary>
    public static IReadOnlyList<string> SplitBatches(string script)
    {
        var withoutUse = UseStatement.Replace(script, string.Empty);
        return GoSeparator.Split(withoutUse)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();
    }

    private static string ReadEmbeddedScript(string resourceName)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded script '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
