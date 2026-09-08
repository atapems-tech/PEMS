using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۲/۳۱: خط‌مبنای Failure Cause - عمومی و
/// مستقل از تجهیز (نه مختص یک خانواده)؛ همان الگوی افزودنی/Idempotent فاز ۱ (بر اساس Code).
/// </summary>
public static class FailureCauseSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("NORMAL_WEAR", "Normal Wear", "فرسایش عادی", 10),
        ("CORROSION", "Corrosion", "خوردگی", 20),
        ("EROSION", "Erosion", "فرسایش سایشی", 30),
        ("FATIGUE", "Fatigue", "خستگی", 40),
        ("MISALIGNMENT", "Misalignment", "عدم هم‌راستایی", 50),
        ("LUBRICATION_FAILURE", "Lubrication Failure", "نقص روانکاری", 60),
        ("OVERLOAD", "Overload", "اضافه‌بار", 70),
        ("OVERHEATING", "Overheating", "گرمای بیش‌ازحد", 80),
        ("CONTAMINATION", "Contamination", "آلودگی", 90),
        ("INSTALLATION_ERROR", "Installation Error", "خطای نصب", 100),
        ("DESIGN_DEFICIENCY", "Design Deficiency", "نقص طراحی", 110),
        ("OPERATING_ERROR", "Operating Error", "خطای بهره‌برداری", 120),
        ("MAINTENANCE_ERROR", "Maintenance Error", "خطای نگهداری و تعمیرات", 130),
        ("ELECTRICAL_FAULT", "Electrical Fault", "خطای الکتریکی", 140),
        ("INSTRUMENTATION_FAULT", "Instrumentation Fault", "خطای ابزار دقیق", 150),
        ("FOREIGN_OBJECT", "Foreign Object", "جسم خارجی", 160),
        ("VIBRATION", "Excessive Vibration Source", "منشأ لرزش بیش‌ازحد", 170),
        ("PROCESS_CONDITION", "Adverse Process Condition", "شرایط نامساعد فرآیندی", 180),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.FailureCauses.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.FailureCauses.Add(new FailureCause
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
