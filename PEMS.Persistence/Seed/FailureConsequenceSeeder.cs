using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۲/۳۱: خط‌مبنای Failure Consequence -
/// دسته‌ی بالادستی پیامد کسب‌وکاری خرابی (ایمنی/محیط‌زیست/تولید/کیفیت/هزینه/بدون‌پیامد). این ۶
/// ردیف خودشان Category های اصلی هستند؛ Category هر ردیف برابر Code خودش تنظیم شده چون این
/// خط‌مبنای سطح‌بالا است (طبق بند ۴۴: تفصیل بیشتر فقط در صورت نیاز واقعی آینده اضافه می‌شود).
/// </summary>
public static class FailureConsequenceSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("SAFETY", "Safety", "ایمنی", 10),
        ("ENVIRONMENT", "Environment", "زیست‌محیطی", 20),
        ("PRODUCTION", "Production", "تولید", 30),
        ("QUALITY", "Quality", "کیفیت", 40),
        ("COST", "Cost", "هزینه", 50),
        ("NONE", "None / Negligible", "بدون‌پیامد / ناچیز", 60),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.FailureConsequences.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.FailureConsequences.Add(new FailureConsequence
            {
                Code = code,
                Name = name,
                NameFa = nameFa,
                Category = code,
                DisplayOrder = order,
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
