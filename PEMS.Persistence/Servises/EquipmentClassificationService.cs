using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification» بند ۹ و ۳۳.
///
/// دیکشنری قاعده‌ی زیر با بررسی مستقیم مقادیر واقعی Equipment.EquipmentType در دیتابیس زنده‌ی
/// تولید (نه فرضی) ساخته شد (SELECT EquipmentType, COUNT(*) FROM Equipments GROUP BY
/// EquipmentType). داده‌ی واقعی بسیار نامنظم است (rشته‌ی آزاد، بدون واژگان ثابت) - فقط مقادیری
/// که به‌صورت قطعی و بدون‌ابهام به یک EquipmentClass واحد اشاره می‌کنند در این دیکشنری قرار
/// گرفتند. مقادیر عمومی/مبهم (DEVICE، CABLE، F&amp;G DEVICE، PANEL، WORKSTATION، package و...) عمداً
/// نگاشت نشدند تا هرگز حدس زده نشود - در گزارش با Applied=false برای بازبینی انسانی می‌مانند.
/// این نگاشت هرگز نباید سکوی حدس زدن باشد؛ فقط یک قاعده‌ی صریح و قطعی است.
/// </summary>
public class EquipmentClassificationService : IEquipmentClassificationService
{
    private readonly PEMSDbContext _context;

    private static readonly (string EquipmentTypeExact, string EquipmentClassCode, string Reason)[] Rules =
    {
        ("pump", "PUMP", "مقدار متن دقیقاً برابر نام عمومی کلاس Pump است"),
        ("pummp", "PUMP", "نگارش اشتباه شناخته‌شده‌ی 'pump' در داده‌ی موجود"),
        ("compressor", "COMPRESSOR", "مقدار متن دقیقاً برابر نام کلاس Compressor است"),
        ("blower", "BLOWER", "مقدار متن دقیقاً برابر نام کلاس Blower است"),
        ("filter", "FILTER", "مقدار متن دقیقاً برابر نام کلاس Filter است"),
        ("reactor", "REACTOR", "مقدار متن دقیقاً برابر نام کلاس Reactor است"),
        ("valve", "VALVE", "مقدار متن دقیقاً برابر نام کلاس Valve است"),
        ("fan", "FAN", "مقدار متن دقیقاً برابر نام کلاس Fan است"),
        ("motor", "MOTOR", "مقدار متن دقیقاً برابر نام کلاس Electric Motor است"),
        ("vessel", "PRESSURE_VESSEL", "مترادف صنعتی استاندارد برای Pressure Vessel"),
        ("drum", "PRESSURE_VESSEL", "در صنعت نفت/گاز، Drum یک مخزن تحت فشار فرآیندی است"),
        ("exchanger", "HEAT_EXCHANGER", "مترادف صنعتی استاندارد برای Heat Exchanger"),
        ("air cooler", "HEAT_EXCHANGER", "Air Cooler یک مبدل حرارتی هوایی (Fin-Fan) است"),
        ("tank", "STORAGE_TANK", "مترادف صنعتی استاندارد برای Storage Tank"),
        ("tower", "COLUMN", "Tower مترادف صنعتی استاندارد برای Column (برج تقطیر) است"),
        ("mixer", "MIXER_AGITATOR", "مقدار متن دقیقاً برابر نام کلاس Mixer/Agitator است"),
        ("agitator", "MIXER_AGITATOR", "مقدار متن دقیقاً برابر نام کلاس Mixer/Agitator است"),
        ("conveyor", "CONVEYOR", "مقدار متن دقیقاً برابر نام کلاس Conveyor است"),
    };

    public EquipmentClassificationService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<EquipmentClassificationReportDto> GetClassificationReportAsync()
    {
        var classesByCode = await _context.EquipmentClasses
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var unclassified = await _context.Equipments
            .AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.EquipmentType })
            .ToListAsync();

        var report = new EquipmentClassificationReportDto
        {
            TotalUnclassifiedEquipment = unclassified.Count
        };

        foreach (var eq in unclassified)
        {
            var normalized = (eq.EquipmentType ?? string.Empty).Trim();
            var rule = Rules.FirstOrDefault(r => string.Equals(r.EquipmentTypeExact, normalized, StringComparison.OrdinalIgnoreCase));

            if (rule != default && classesByCode.TryGetValue(rule.EquipmentClassCode, out var cls))
            {
                report.Suggestions.Add(new EquipmentClassificationSuggestionDto
                {
                    EquipmentId = eq.EquipmentId,
                    TagNumber = eq.TagNumber,
                    CurrentEquipmentType = eq.EquipmentType,
                    ProposedEquipmentClassId = cls.EquipmentClassId,
                    ProposedEquipmentClassCode = cls.Code,
                    ProposedEquipmentClassName = cls.Name,
                    Confidence = "EXACT",
                    Reason = rule.Reason,
                    Applied = false
                });
                report.MatchedByRule++;
            }
            else
            {
                report.Suggestions.Add(new EquipmentClassificationSuggestionDto
                {
                    EquipmentId = eq.EquipmentId,
                    TagNumber = eq.TagNumber,
                    CurrentEquipmentType = eq.EquipmentType,
                    Reason = "هیچ قاعده‌ی قطعی و بدون‌ابهامی برای این EquipmentType یافت نشد - نیازمند بازبینی انسانی",
                    Applied = false
                });
                report.UnmatchedNeedsReview++;
            }
        }

        return report;
    }

    public async Task<int> BackfillAsync()
    {
        var classesByCode = await _context.EquipmentClasses
            .ToDictionaryAsync(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        // طبق بند ۳۰: فقط رکوردهایی که هنوز EquipmentClassId=Null دارند لمس می‌شوند - هرگز یک
        // انتساب موجود (دستی یا خودکار قبلی) بازنویسی نمی‌شود.
        var candidates = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .ToListAsync();

        var appliedCount = 0;

        foreach (var eq in candidates)
        {
            var normalized = (eq.EquipmentType ?? string.Empty).Trim();
            var rule = Rules.FirstOrDefault(r => string.Equals(r.EquipmentTypeExact, normalized, StringComparison.OrdinalIgnoreCase));

            if (rule == default || !classesByCode.TryGetValue(rule.EquipmentClassCode, out var cls))
                continue;

            eq.EquipmentClassId = cls.EquipmentClassId;
            eq.ClassificationConfidence = "EXACT";
            eq.ClassificationSource = "RULE";
            appliedCount++;
        }

        if (appliedCount > 0)
            await _context.SaveChangesAsync();

        return appliedCount;
    }
}
