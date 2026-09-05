using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق سند Inventory بخش ۶: دسته‌بندی سطح بالای متریال (Category ≠ Discipline) - ترکیبی از
/// نمونه‌ی خودِ سند و دسته‌های واقعی دیده‌شده در فایل‌های MRS (Chemical/Civil/General/Tank).
/// </summary>
public static class MaterialCategorySeeder
{
    private static readonly (string Code, string Name)[] Defaults =
    {
        ("MECH", "Mechanical"),
        ("ELEC", "Electrical"),
        ("INST", "Instrumentation"),
        ("SAFT", "Safety"),
        ("CHEM", "Chemical"),
        ("CIVL", "Civil"),
        ("GEN", "General"),
        ("TANK", "Tank & Storage"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.MaterialCategories.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name) in Defaults)
        {
            if (existingCodes.Contains(code)) continue;

            db.MaterialCategories.Add(new MaterialCategory
            {
                Code = code,
                Name = name,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
