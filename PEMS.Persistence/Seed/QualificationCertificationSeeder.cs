using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق درخواست: صلاحیت‌ها و گواهینامه‌های استاندارد رایج صنعت نفت/گاز/پتروشیمی به‌عنوان
/// نقطه‌ی شروع - قابل گسترش از همان صفحه‌ی Maintenance Master Data.
/// </summary>
public static class QualificationSeeder
{
    private static readonly (string? DisciplineCode, string Name)[] Defaults =
    {
        ("MECH", "Qualified Pump Maintenance Technician"),
        ("ELEC", "Qualified Electrical Technician"),
        ("INST", "Qualified Instrument Technician"),
        (null, "Confined Space Entry Authorized"),
        (null, "Working at Height Authorized"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var disciplines = await db.Disciplines.Where(x => x.IsActive).ToListAsync();

        var existingNames = (await db.Qualifications.Select(x => x.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (disciplineCode, name) in Defaults)
        {
            if (existingNames.Contains(name)) continue;

            var discipline = disciplineCode == null ? null : disciplines.FirstOrDefault(x => x.Code == disciplineCode);

            db.Qualifications.Add(new Qualification
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

/// <summary>
/// طبق بخش ۱۹ سند (مثال «NDT Level II») - سطوح استاندارد ASNT برای NDT و گواهی‌های ایمنی/
/// جوشکاری رایج در پروژه‌های نفت/گاز/پتروشیمی.
/// </summary>
public static class CertificationSeeder
{
    private static readonly (string? DisciplineCode, string Name)[] Defaults =
    {
        (null, "NDT Level I"),
        (null, "NDT Level II"),
        ("MECH", "Welding Certificate (ASME IX)"),
        (null, "Confined Space Entry"),
        (null, "Working at Height"),
        (null, "First Aid"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var disciplines = await db.Disciplines.Where(x => x.IsActive).ToListAsync();

        var existingNames = (await db.Certifications.Select(x => x.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (disciplineCode, name) in Defaults)
        {
            if (existingNames.Contains(name)) continue;

            var discipline = disciplineCode == null ? null : disciplines.FirstOrDefault(x => x.Code == disciplineCode);

            db.Certifications.Add(new Certification
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
