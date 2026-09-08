using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۲: پیوند Many-to-Many سراسری Cause↔
/// Mechanism (نه مختص یک خانواده‌ی تجهیز) - چون یک Cause (مثلاً Corrosion) می‌تواند از چند
/// Mechanism فیزیکی متفاوت ناشی شود و برعکس. باید بعد از FailureCauseSeeder و
/// FailureMechanismSeeder اجرا شود.
/// </summary>
public static class FailureCauseMechanismSeeder
{
    private static readonly (string CauseCode, string MechanismCode)[] Links =
    {
        ("CORROSION", "GENERAL_CORROSION"),
        ("CORROSION", "LOCALIZED_CORROSION"),
        ("CORROSION", "PITTING"),
        ("EROSION", "EROSION"),
        ("EROSION", "EROSION_CORROSION"),
        ("EROSION", "CAVITATION"),
        ("FATIGUE", "FATIGUE"),
        ("FATIGUE", "THERMAL_FATIGUE"),
        ("NORMAL_WEAR", "WEAR"),
        ("NORMAL_WEAR", "FRETTING"),
        ("OVERLOAD", "BRITTLE_FRACTURE"),
        ("OVERLOAD", "FATIGUE"),
        ("OVERHEATING", "THERMAL_FATIGUE"),
        ("OVERHEATING", "CREEP"),
        ("OVERHEATING", "OVERHEATING"),
        ("OVERHEATING", "ELECTRICAL_BREAKDOWN"),
        ("CONTAMINATION", "PITTING"),
        ("CONTAMINATION", "EROSION_CORROSION"),
        ("PROCESS_CONDITION", "STRESS_CORROSION_CRACKING"),
        ("PROCESS_CONDITION", "HYDROGEN_DAMAGE"),
        ("ELECTRICAL_FAULT", "ELECTRICAL_BREAKDOWN"),
        ("MISALIGNMENT", "FRETTING"),
        ("MISALIGNMENT", "WEAR"),
        ("LUBRICATION_FAILURE", "WEAR"),
        ("LUBRICATION_FAILURE", "FRETTING"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var causesByCode = await db.FailureCauses.ToDictionaryAsync(x => x.Code, x => x.FailureCauseId, StringComparer.OrdinalIgnoreCase);
        var mechanismsByCode = await db.FailureMechanisms.ToDictionaryAsync(x => x.Code, x => x.FailureMechanismId, StringComparer.OrdinalIgnoreCase);

        var existing = (await db.FailureCauseMechanisms
                .Select(x => new { x.FailureCauseId, x.FailureMechanismId })
                .ToListAsync())
            .Select(x => (x.FailureCauseId, x.FailureMechanismId))
            .ToHashSet();

        var added = false;

        foreach (var (causeCode, mechanismCode) in Links)
        {
            if (!causesByCode.TryGetValue(causeCode, out var causeId)) continue;
            if (!mechanismsByCode.TryGetValue(mechanismCode, out var mechanismId)) continue;
            if (existing.Contains((causeId, mechanismId))) continue;

            db.FailureCauseMechanisms.Add(new FailureCauseMechanism
            {
                FailureCauseId = causeId,
                FailureMechanismId = mechanismId
            });
            existing.Add((causeId, mechanismId));
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }
}
