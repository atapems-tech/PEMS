using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق ممیزی ۹ فایل واقعی MRS: پیشوندهای واقعی موجود در ستون MRS NO (WH-EL، WH-CH، HV،
/// WH-SF، WH-TANK، WH-ME، WH-GEN، WH-CV) - نه حدسی، دقیقاً همان ۸ انبار واقعی که در داده
/// دیده شد.
/// </summary>
public static class WarehouseSeeder
{
    private static readonly (string Code, string Name)[] Defaults =
    {
        ("WH-EL", "انبار الکتریکال"),
        ("WH-CH", "انبار شیمیایی"),
        ("WH-HV", "انبار عایق/HV"),
        ("WH-SF", "انبار ایمنی"),
        ("WH-TANK", "انبار تانک"),
        ("WH-ME", "انبار مکانیکال"),
        ("WH-GEN", "انبار عمومی"),
        ("WH-CV", "انبار سیویل"),
        ("WH-IN", "انبار ابزار دقیق"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.Warehouses.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name) in Defaults)
        {
            if (existingCodes.Contains(code)) continue;

            db.Warehouses.Add(new Warehouse
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
