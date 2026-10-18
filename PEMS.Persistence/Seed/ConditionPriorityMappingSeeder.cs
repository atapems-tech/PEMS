using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.7: این مقادیر دقیقاً همون نگاشت
/// Hard-Code قبلی FindingController.RaiseWorkRequest هستند - رفتار پیش‌فرض عوض نمی‌شود، فقط از
/// کد C# به یک ردیف پایگاه‌داده‌ی قابل‌مشاهده/قابل‌ویرایش منتقل می‌شود (طبق بند ۲۲ سند).
/// </summary>
public static class ConditionPriorityMappingSeeder
{
    private static readonly (string Condition, string Priority)[] Defaults =
    {
        ("Critical", "Critical"),
        ("Reject", "Critical"),
        ("Alarm", "High"),
        ("Poor", "High"),
        ("Warning", "Medium"),
        ("Fair", "Medium"),
        ("NotEvaluated", "Medium"),
        ("Normal", "Low"),
        ("Good", "Low"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existing = (await db.ConditionPriorityMappings.Select(x => x.Condition).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existing.Contains(d.Condition)).ToList();
        if (missing.Count == 0) return;

        foreach (var (condition, priority) in missing)
        {
            db.ConditionPriorityMappings.Add(new ConditionPriorityMapping
            {
                Condition = condition,
                Priority = priority,
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
