using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class DatabaseUpdateService : IDatabaseUpdateService
{
    // One migration run per process at a time; EF Core's own migration lock covers other instances.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly PEMSDbContext _db;
    private readonly ILogger<DatabaseUpdateService> _logger;

    public DatabaseUpdateService(PEMSDbContext db, ILogger<DatabaseUpdateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DatabaseUpdateStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var known = _db.Database.GetMigrations().ToList();
        var dto = new DatabaseUpdateStatusDto
        {
            KnownMigrationCount = known.Count,
            LatestKnownMigration = known.LastOrDefault()
        };

        bool canConnect;
        try { canConnect = await _db.Database.CanConnectAsync(ct); }
        catch { canConnect = false; }

        if (!canConnect)
        {
            dto.State = DatabaseSetupState.Unreachable;
            dto.Summary = "The database server or database cannot be reached. Check the connection string and that the database exists.";
            return dto;
        }

        var applied = (await _db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var hasUserTables = await HasUserTablesAsync(ct);

        dto.AppliedMigrationCount = applied.Count;
        dto.LastAppliedMigration = applied.LastOrDefault();
        dto.State = DatabaseSetupClassifier.Classify(hasUserTables, applied, known);

        var appliedSet = new HashSet<string>(applied, StringComparer.Ordinal);
        dto.PendingMigrations = known.Where(k => !appliedSet.Contains(k)).ToList();
        dto.Summary = Describe(dto);

        if (applied.Count > 0)
            dto.RecentRuns = await LoadRecentRunsAsync(ct);

        return dto;
    }

    public async Task<DatabaseUpdateResultDto> ApplyAsync(string trigger, string? performedBy, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var status = await GetStatusAsync(ct);
            var result = new DatabaseUpdateResultDto { StateBefore = status.State, StateAfter = status.State };

            switch (status.State)
            {
                case DatabaseSetupState.UpToDate:
                    result.Succeeded = true;
                    result.NothingToDo = true;
                    result.Message = "The database is already up to date.";
                    return result;
                case DatabaseSetupState.Unreachable:
                case DatabaseSetupState.UnmanagedSchema:
                case DatabaseSetupState.DatabaseNewerThanApplication:
                    result.Message = status.Summary;
                    return result;
            }

            var mode = status.State == DatabaseSetupState.FirstRun ? "FirstRun" : "Update";
            var started = DateTime.UtcNow;
            string? error = null;
            try
            {
                _logger.LogInformation("Database {Mode}: applying {Count} migration(s), latest {Latest}.",
                    mode, status.PendingMigrations.Count, status.LatestKnownMigration);
                _db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
                await _db.Database.MigrateAsync(ct);
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                _logger.LogError(ex, "Database {Mode} failed.", mode);
            }

            var succeeded = error is null;
            result.Succeeded = succeeded;
            result.MigrationsApplied = succeeded ? status.PendingMigrations.Count : 0;
            result.Message = succeeded
                ? (mode == "FirstRun"
                    ? $"Database installed ({status.PendingMigrations.Count} migrations applied)."
                    : $"Database updated ({status.PendingMigrations.Count} migration(s) applied).")
                : "Database " + (mode == "FirstRun" ? "install" : "update") + " failed: " + error;

            await RecordRunAsync(new DatabaseUpdateRun
            {
                StartedAtUtc = started,
                CompletedAtUtc = DateTime.UtcNow,
                Trigger = trigger,
                Mode = mode,
                FromMigration = status.LastAppliedMigration,
                ToMigration = succeeded ? status.LatestKnownMigration : status.LastAppliedMigration,
                MigrationsApplied = result.MigrationsApplied,
                Succeeded = succeeded,
                Error = error is null ? null : Truncate(error, 2000),
                PerformedBy = performedBy
            }, ct);

            result.StateAfter = (await GetStatusAsync(ct)).State;
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string Describe(DatabaseUpdateStatusDto s) => s.State switch
    {
        DatabaseSetupState.FirstRun =>
            $"The database is empty. Applying will install the schema ({s.PendingMigrations.Count} migrations).",
        DatabaseSetupState.UpdateAvailable =>
            $"{s.PendingMigrations.Count} update(s) are pending. Take a backup, then apply.",
        DatabaseSetupState.UpToDate =>
            "The database is up to date with this application version.",
        DatabaseSetupState.UnmanagedSchema =>
            "The database has tables but no migration history (it was not created by this migrator). It is never changed automatically; restore it into an empty database or baseline it manually.",
        DatabaseSetupState.DatabaseNewerThanApplication =>
            "The database has migrations this application version does not contain. Deploy the matching or newer application version.",
        _ => "Unknown."
    };

    private async Task<bool> HasUserTablesAsync(CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            // dbo only: Hangfire installs its own [HangFire] schema before the migrator runs.
            command.CommandText =
                "SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = N'dbo' AND name NOT LIKE N'[_][_]EF%'";
            return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    private async Task<List<DatabaseUpdateRunDto>> LoadRecentRunsAsync(CancellationToken ct)
    {
        try
        {
            return await _db.DatabaseUpdateRuns.AsNoTracking()
                .OrderByDescending(r => r.StartedAtUtc)
                .Take(20)
                .Select(r => new DatabaseUpdateRunDto
                {
                    StartedAtUtc = r.StartedAtUtc,
                    DurationSeconds = (r.CompletedAtUtc - r.StartedAtUtc).TotalSeconds,
                    Trigger = r.Trigger,
                    Mode = r.Mode,
                    FromMigration = r.FromMigration,
                    ToMigration = r.ToMigration,
                    MigrationsApplied = r.MigrationsApplied,
                    Succeeded = r.Succeeded,
                    Error = r.Error,
                    PerformedBy = r.PerformedBy
                })
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            // The history table arrives with a migration; before that there is nothing to show.
            _logger.LogDebug(ex, "Database update history is not available yet.");
            return new List<DatabaseUpdateRunDto>();
        }
    }

    private async Task RecordRunAsync(DatabaseUpdateRun run, CancellationToken ct)
    {
        try
        {
            _db.DatabaseUpdateRuns.Add(run);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // A failed run can leave the history table missing; the log above already holds the outcome.
            _db.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Could not record the database update run.");
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
