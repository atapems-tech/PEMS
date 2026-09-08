using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — PM Template Architecture» بند ۲۱/۳۱: خط‌مبنای کوچک و کاملاً موجه (نه یک
/// کاتالوگ ساختگی کامل) - عمداً JobPlanId هر ردیف خط‌مبنایی Null می‌ماند، چون تولید یک
/// Job Plan واقعی (با Task/Material/Skill مشخص) بدون دانش واقعی سایت به معنای فرضی‌سازی نگهداری
/// (Fabricating fake PM plans) خواهد بود که طبق بند ۴۴ صریحاً ممنوع است. TriggerType/IntervalDays/
/// MeterType/MeterInterval دقیقاً از همان الگوی MaintenancePlan (فاز موجود) تبعیت می‌کند تا
/// تبدیل PMTemplate → MaintenancePlan یک Copy ساده باشد، نه Translation.
/// </summary>
public static class PMTemplateSeeder
{
    private static readonly (string Code, string Name, string NameFa, string EquipmentClassCode, string? StrategyTypeCode, string? MaintenanceTypeCode, string TriggerType, int? IntervalDays, string? StandardReferenceCode, string? ApplicableCriticality, int Order)[] Defaults =
    {
        ("PMT_PUMP_CENT_MONTHLY", "Centrifugal Pump - Monthly PM", "نگهداری پیشگیرانه ماهانه پمپ سانتریفیوژ",
            "PUMP_CENT", "PM", "PREVENTIVE", "Calendar", 30, "ISO-14224", "A,B", 10),
        ("PMT_MOTOR_QUARTERLY", "Electric Motor - Quarterly PM", "نگهداری پیشگیرانه سه‌ماهه موتور الکتریکی",
            "MOTOR", "PM", "PREVENTIVE", "Calendar", 90, null, "A,B,C", 20),
        ("PMT_COMPRESSOR_MONTHLY", "Compressor - Monthly PM", "نگهداری پیشگیرانه ماهانه کمپرسور",
            "COMPRESSOR", "PM", "PREVENTIVE", "Calendar", 30, "ISO-14224", "A,B", 30),
        ("PMT_PRESSURE_VESSEL_ANNUAL_INSPECTION", "Pressure Vessel - Annual Inspection", "بازرسی سالانه مخزن تحت فشار",
            "PRESSURE_VESSEL", "PM", "INSPECTION", "Calendar", 365, "API-510", "A,B,C", 40),
        ("PMT_STORAGE_TANK_ANNUAL_INSPECTION", "Storage Tank - Annual External Inspection", "بازرسی بیرونی سالانه تانک ذخیره",
            "STORAGE_TANK", "PM", "INSPECTION", "Calendar", 365, "API-653", "A,B,C", 50),
        ("PMT_GEARBOX_QUARTERLY", "Gearbox - Quarterly PM", "نگهداری پیشگیرانه سه‌ماهه گیربکس",
            "GEARBOX", "PM", "PREVENTIVE", "Calendar", 90, null, "A,B", 60),
        ("PMT_HEAT_EXCHANGER_SEMIANNUAL", "Heat Exchanger - Semi-Annual Cleaning/Inspection", "بازرسی و تمیزکاری شش‌ماهه مبدل حرارتی",
            "HEAT_EXCHANGER", "PM", "PREVENTIVE", "Calendar", 180, null, "A,B", 70),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var classesByCode = await db.EquipmentClasses.ToDictionaryAsync(x => x.Code, x => x.EquipmentClassId, StringComparer.OrdinalIgnoreCase);
        var strategyTypesByCode = await db.MaintenanceStrategyTypes.ToDictionaryAsync(x => x.Code, x => x.MaintenanceStrategyTypeId, StringComparer.OrdinalIgnoreCase);
        var maintenanceTypesByCode = await db.MaintenanceTypes.ToDictionaryAsync(x => x.Code, x => x.MaintenanceTypeId, StringComparer.OrdinalIgnoreCase);
        var standardsByCode = await db.StandardReferences.ToDictionaryAsync(x => x.Code, x => x.StandardReferenceId, StringComparer.OrdinalIgnoreCase);

        var existingCodes = (await db.PMTemplates.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        foreach (var (code, name, nameFa, classCode, strategyCode, maintTypeCode, triggerType, intervalDays, standardCode, criticality, order) in missing)
        {
            if (!classesByCode.TryGetValue(classCode, out var classId)) continue; // بدون کلاس معتبر، Template ساخته نمی‌شود

            db.PMTemplates.Add(new PMTemplate
            {
                Code = code,
                Name = name,
                NameFa = nameFa,
                EquipmentClassId = classId,
                MaintenanceStrategyTypeId = strategyCode != null && strategyTypesByCode.TryGetValue(strategyCode, out var stId) ? stId : null,
                MaintenanceTypeId = maintTypeCode != null && maintenanceTypesByCode.TryGetValue(maintTypeCode, out var mtId) ? mtId : null,
                JobPlanId = null,
                TriggerType = triggerType,
                IntervalDays = intervalDays,
                StandardReferenceId = standardCode != null && standardsByCode.TryGetValue(standardCode, out var srId) ? srId : null,
                ApplicableCriticalityClasses = criticality,
                DisplayOrder = order,
                IsSystemDefault = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
