using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>طبق ممیزی فایل‌های MRS: واحدهای واقعی دیده‌شده در ستون UNT/UOM (LIT/LT/KG/PCS/NO/MT/ROLL...).</summary>
public static class MaterialUOMSeeder
{
    private static readonly (string Code, string Name)[] Defaults =
    {
        ("PCS", "Piece"),
        ("SET", "Set"),
        ("KG", "Kilogram"),
        ("MT", "Metric Ton"),
        ("LT", "Liter"),
        ("MTR", "Meter"),
        ("ROLL", "Roll"),
        ("BOX", "Box"),
        ("PAIR", "Pair"),
        ("NO", "Number"),
        ("DRUM", "Drum"),
        ("BAG", "Bag"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.MaterialUOMs.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name) in Defaults)
        {
            if (existingCodes.Contains(code)) continue;

            db.MaterialUOMs.Add(new MaterialUOM
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
