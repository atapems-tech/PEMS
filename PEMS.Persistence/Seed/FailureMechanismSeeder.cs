using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۲/۳۱: خط‌مبنای Failure Mechanism
/// (چگونگی فیزیکی وقوع خرابی - سطح جزئی‌تر از Cause، طبق API 571/ISO 14224).
/// </summary>
public static class FailureMechanismSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("GENERAL_CORROSION", "General Corrosion", "خوردگی عمومی", 10),
        ("LOCALIZED_CORROSION", "Localized Corrosion", "خوردگی موضعی", 20),
        ("PITTING", "Pitting", "خوردگی حفره‌ای", 30),
        ("EROSION", "Erosion", "فرسایش سایشی", 40),
        ("EROSION_CORROSION", "Erosion-Corrosion", "خوردگی-فرسایشی", 50),
        ("CAVITATION", "Cavitation", "کاویتاسیون", 60),
        ("FATIGUE", "Mechanical Fatigue", "خستگی مکانیکی", 70),
        ("THERMAL_FATIGUE", "Thermal Fatigue", "خستگی حرارتی", 80),
        ("CREEP", "Creep", "خزش", 90),
        ("BRITTLE_FRACTURE", "Brittle Fracture", "شکست ترد", 100),
        ("STRESS_CORROSION_CRACKING", "Stress Corrosion Cracking", "ترک خوردگی تنشی", 110),
        ("HYDROGEN_DAMAGE", "Hydrogen Damage", "آسیب هیدروژنی", 120),
        ("WEAR", "Mechanical Wear", "سایش مکانیکی", 130),
        ("FRETTING", "Fretting", "فرتینگ (سایش لرزشی)", 140),
        ("OVERHEATING", "Overheating Damage", "آسیب ناشی از حرارت بیش‌ازحد", 150),
        ("ELECTRICAL_BREAKDOWN", "Electrical Breakdown", "شکست عایقی/الکتریکی", 160),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.FailureMechanisms.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.FailureMechanisms.Add(new FailureMechanism
            {
                Code = code,
                Name = name,
                NameFa = nameFa,
                DisplayOrder = order,
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
