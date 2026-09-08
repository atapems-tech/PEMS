using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۶: خط‌مبنای
/// استاندارد Maintenance Type - بررسی به‌ازای هر Code (نه AnyAsync سراسری)، Additive/Idempotent.
/// </summary>
public static class MaintenanceTypeSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("CORRECTIVE", "Corrective", "اصلاحی", 10),
        ("PREVENTIVE", "Preventive", "پیشگیرانه", 20),
        ("PREDICTIVE", "Predictive", "پیش‌بینانه", 30),
        ("CONDITION_BASED", "Condition Based", "مبتنی بر وضعیت", 40),
        ("EMERGENCY", "Emergency", "اضطراری", 50),
        ("BREAKDOWN", "Breakdown", "خرابی ناگهانی", 60),
        ("OPPORTUNITY", "Opportunity", "فرصت‌طلبانه", 70),
        ("SHUTDOWN", "Shutdown", "توقف کامل", 80),
        ("TURNAROUND", "Turnaround", "توقف دوره‌ای (Turnaround)", 90),
        ("OVERHAUL", "Overhaul", "بازسازی کامل", 100),
        ("MODIFICATION", "Modification", "اصلاح/تغییر", 110),
        ("INSPECTION", "Inspection", "بازرسی", 120),
        ("TESTING", "Testing", "تست", 130),
        ("CALIBRATION", "Calibration", "کالیبراسیون", 140),
        ("LUBRICATION", "Lubrication", "روانکاری", 150),
        ("CLEANING", "Cleaning", "نظافت/شست‌وشو", 160),
        ("ALIGNMENT", "Alignment", "هم‌راستاسازی", 170),
        ("FUNCTIONAL_TEST", "Functional Test", "تست عملکردی", 180),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.MaintenanceTypes.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.MaintenanceTypes.Add(new MaintenanceType
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
