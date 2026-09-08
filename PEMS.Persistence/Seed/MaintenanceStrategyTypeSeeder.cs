using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۹: خط‌مبنای
/// سیاست RCM (Maintenance Strategy Type) - مستقل از MaintenanceType بالا (که «نوع کار» را
/// طبقه‌بندی می‌کند، نه «چرا این سیاست انتخاب شد»).
/// </summary>
public static class MaintenanceStrategyTypeSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("PM", "Preventive Maintenance", "نگهداری پیشگیرانه", 10),
        ("PDM", "Predictive Maintenance", "نگهداری پیش‌بینانه", 20),
        ("CBM", "Condition Based Maintenance", "نگهداری مبتنی بر وضعیت", 30),
        ("CM", "Corrective Maintenance", "نگهداری اصلاحی", 40),
        ("EM", "Emergency Maintenance", "نگهداری اضطراری", 50),
        ("RTF", "Run to Failure", "اجرا تا خرابی", 60),
        ("RCM", "Reliability Centered Maintenance", "نگهداری متمرکز بر قابلیت اطمینان", 70),
        ("SHUTDOWN", "Shutdown Maintenance", "نگهداری در زمان توقف", 80),
        ("TURNAROUND", "Turnaround Maintenance", "نگهداری دوره‌ای (Turnaround)", 90),
        ("OVERHAUL", "Overhaul", "بازسازی کامل", 100),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.MaintenanceStrategyTypes.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.MaintenanceStrategyTypes.Add(new MaintenanceStrategyType
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
