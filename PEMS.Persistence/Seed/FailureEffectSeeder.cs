using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۲/۳۱: خط‌مبنای Failure Effect
/// (پیامد مستقیم و مشهود خرابی بر عملکرد تجهیز).
/// </summary>
public static class FailureEffectSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("LOSS_OF_FUNCTION", "Loss of Function", "از دست رفتن کامل عملکرد", 10),
        ("DEGRADED_FUNCTION", "Degraded Function", "کاهش کیفیت عملکرد", 20),
        ("REDUCED_CAPACITY", "Reduced Capacity", "کاهش ظرفیت", 30),
        ("INCREASED_ENERGY_CONSUMPTION", "Increased Energy Consumption", "افزایش مصرف انرژی", 40),
        ("LEAKAGE", "Leakage", "نشتی", 50),
        ("SAFETY_IMPACT", "Safety Impact", "تأثیر ایمنی", 60),
        ("ENVIRONMENTAL_IMPACT", "Environmental Impact", "تأثیر زیست‌محیطی", 70),
        ("PRODUCTION_LOSS", "Production Loss", "توقف/کاهش تولید", 80),
        ("QUALITY_IMPACT", "Quality Impact", "تأثیر بر کیفیت محصول", 90),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.FailureEffects.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.FailureEffects.Add(new FailureEffect
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
