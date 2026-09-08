using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۸: خط‌مبنای
/// طبقه‌بندی تجهیزات (Equipment Class) - فقط برای تجهیزات دوّار/ثابت پرکاربردی که در بند ۴/۸
/// سند و مثال‌های موجود پروژه (Pump/Compressor/Fan/Blower/Gearbox/Turbine/Motor) ذکر شده‌اند؛
/// طبق بند ۲۳ («arbitrary maintenance categories without engineering justification» ممنوع)،
/// از گسترش دلخواه این لیست پرهیز شد.
/// </summary>
public static class EquipmentClassSeeder
{
    private static readonly (string Code, string Name, string NameFa, int Order)[] Defaults =
    {
        ("PUMP_CENT", "Centrifugal Pump", "پمپ سانتریفیوژ", 10),
        ("PUMP_RECIP", "Reciprocating Pump", "پمپ رفت‌وبرگشتی", 20),
        ("COMPRESSOR", "Compressor", "کمپرسور", 30),
        ("FAN", "Fan", "فن", 40),
        ("BLOWER", "Blower", "بلوئر", 50),
        ("GEARBOX", "Gearbox", "گیربکس", 60),
        ("TURBINE", "Turbine", "توربین", 70),
        ("MOTOR", "Electric Motor", "موتور الکتریکی", 80),
        ("PRESSURE_VESSEL", "Pressure Vessel", "مخزن تحت فشار", 90),
        ("HEAT_EXCHANGER", "Heat Exchanger", "مبدل حرارتی", 100),
        ("STORAGE_TANK", "Storage Tank", "تانک ذخیره", 110),
        ("PIPING", "Piping", "خط لوله", 120),
        ("VALVE", "Valve", "شیر (والو)", 130),
        ("TRANSFORMER", "Transformer", "ترانسفورماتور", 140),
        ("SWITCHGEAR", "Switchgear", "سوئیچ‌گیر", 150),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.EquipmentClasses.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, order) in missing)
        {
            db.EquipmentClasses.Add(new EquipmentClass
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
