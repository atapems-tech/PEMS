using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Application.Migration;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>The migrator center operations (SCRUM-582); see <see cref="IMigratorCenter"/>.</summary>
public sealed class MigratorCenter(IConfiguration configuration, PEMSDbContext db, ILogger<MigratorCenter> log) : IMigratorCenter
{
    public async Task<MigratorResult> InspectAsync(byte[] package, string actor)
    {
        var keys = configuration.GetSection("Migrator:TrustedKeys").GetChildren().Where(c => !string.IsNullOrWhiteSpace(c.Value)).ToDictionary(c => c.Key, c => c.Value!, StringComparer.Ordinal);
        if (keys.Count == 0) return new(false, "No vendor key is configured (Migrator:TrustedKeys), so a package cannot be trusted.");
        try
        {
            var (manifest, files, hash) = MigrationPackage.Read(package, new TrustedKeys(keys));
            var empty = !await db.Definitions.AsNoTracking().AnyAsync();
            var version = typeof(IMigratorCenter).Assembly.GetName().Version?.ToString(3);
            var plan = MigrationPackage.Detect(manifest, hash, new DatabaseState(empty, version, null, new HashSet<string>()));
            var lines = new List<string> { $"{plan.Action}: {plan.Reason}", $"Package {manifest.Kind} to version {manifest.ToVersion}, {files.Count} files, {manifest.Definitions.Count} definitions." };
            lines.AddRange(manifest.DataSets.Select(d => $"Data set {d.Entity}: {d.Rows} rows, mode {d.Mode}."));
            log.LogInformation("Migrator inspect by {Actor}: {Action} package {Hash}", actor, plan.Action, hash[..12]);
            return new(plan.Action != PackageAction.Refused, string.Join(" ", lines));
        }
        catch (PackageRefusedException e)
        {
            log.LogWarning("Migrator inspect by {Actor} refused a package: {Reason}", actor, e.Message);
            return new(false, e.Message);
        }
    }
}
