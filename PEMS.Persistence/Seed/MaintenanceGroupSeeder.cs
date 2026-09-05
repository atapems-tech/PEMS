using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق درخواست: Maintenance Master Data باید به‌صورت پیش‌فرض بر اساس استانداردهای رایج
/// صنعت نفت/گاز/پتروشیمی آماده باشد - طبقه‌بندی زیرمجموعه‌های تعمیراتی هر دیسیپلین طبق
/// PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش‌های ۵ تا ۸ (Rotating/Static Equipment،
/// MCC/Switchgear، PLC/DCS/SIS، Pipeline/Flange...). فقط برای دیسیپلین‌هایی seed می‌شود که
/// واقعاً در سیستم موجودند - طبق بخش ۴ سند: «این‌ها کاندید معماری‌اند، نه اجازه‌ی seed کورکورانه».
/// </summary>
public static class MaintenanceGroupSeeder
{
    private static readonly (string DisciplineCode, string Code, string Name)[] Defaults =
    {
        ("MECH", "MECH-ROT", "Rotating Equipment"),
        ("MECH", "MECH-STA", "Static Equipment"),
        ("MECH", "MECH-VLV", "Mechanical Valves"),
        ("MECH", "MECH-WKS", "Workshop & Machining"),
        ("MECH", "MECH-FAB", "Fabrication"),

        ("ELEC", "ELEC-MOT", "Motors"),
        ("ELEC", "ELEC-MCC", "MCC"),
        ("ELEC", "ELEC-SWG", "Switchgear"),
        ("ELEC", "ELEC-TRF", "Transformers"),
        ("ELEC", "ELEC-PWR", "Power Distribution"),
        ("ELEC", "ELEC-CAB", "Cables"),
        ("ELEC", "ELEC-PRO", "Protection"),
        ("ELEC", "ELEC-UPS", "UPS & Battery"),

        ("INST", "INST-FLD", "Field Instruments"),
        ("INST", "INST-CV", "Control Valves"),
        ("INST", "INST-PLC", "PLC"),
        ("INST", "INST-DCS", "DCS"),
        ("INST", "INST-SIS", "SIS"),
        ("INST", "INST-ANL", "Analyzers"),
        ("INST", "INST-CAL", "Calibration"),
        ("INST", "INST-FG", "Fire & Gas"),

        ("PIPE", "PIPE-LIN", "Pipeline"),
        ("PIPE", "PIPE-FLG", "Flange Management"),
        ("PIPE", "PIPE-SUP", "Piping Supports"),
        ("PIPE", "PIPE-RPR", "Piping Repair"),
        ("PIPE", "PIPE-HW", "Hot Work"),

        ("CIVL", "CIVL-STR", "Civil Structures & Buildings"),
        ("CIVL", "CIVL-RD", "Roads & Drainage"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var disciplines = await db.Disciplines.Where(x => x.IsActive).ToListAsync();
        if (disciplines.Count == 0) return;

        // طبق درخواست: پر کردن پیش‌فرض‌های استاندارد، بدون دست‌زدن به مواردی که کاربر خودش
        // قبلاً از همین صفحه ساخته - چک به‌ازای هر مورد، نه یک‌بار برای کل جدول
        var existingCodes = (await db.MaintenanceGroups.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (disciplineCode, code, name) in Defaults)
        {
            if (existingCodes.Contains(code)) continue;

            var discipline = disciplines.FirstOrDefault(x => x.Code == disciplineCode);
            if (discipline == null) continue;

            db.MaintenanceGroups.Add(new MaintenanceGroup
            {
                Code = code,
                Name = name,
                DisciplineId = discipline.DisciplineId,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
