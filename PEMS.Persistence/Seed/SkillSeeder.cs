using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۶ - همان فهرست نمونه‌ی خودِ سند،
/// عیناً. NDT عمداً بدون دیسیپلین است چون بین چند دیسیپلین مشترک است (بازرسی/یکپارچگی).
/// </summary>
public static class SkillSeeder
{
    private static readonly (string? DisciplineCode, string Name)[] Defaults =
    {
        ("MECH", "Pump Alignment"),
        ("MECH", "Vibration Analysis"),
        ("MECH", "Mechanical Seal Replacement"),
        ("MECH", "Welding"),
        (null, "NDT"),
        ("INST", "Pressure Calibration"),
        ("INST", "Temperature Calibration"),
        ("INST", "DCS"),
        ("INST", "PLC"),
        ("INST", "SIS"),
        ("ELEC", "Electrical Protection"),
        ("ELEC", "Motor Testing"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var disciplines = await db.Disciplines.Where(x => x.IsActive).ToListAsync();

        var existingNames = (await db.Skills.Select(x => x.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (disciplineCode, name) in Defaults)
        {
            if (existingNames.Contains(name)) continue;

            var discipline = disciplineCode == null ? null : disciplines.FirstOrDefault(x => x.Code == disciplineCode);

            db.Skills.Add(new Skill
            {
                Name = name,
                DisciplineId = discipline?.DisciplineId,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
