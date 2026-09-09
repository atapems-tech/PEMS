using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 5» بند ۱۴/۲۱/۲۳: روی IReliabilityService موجود (فرمول‌های تاییدشده‌ی
/// MTBF/MTTR/Availability فاز قبلی) سوار می‌شود - هرگز محاسبه‌ی موازی نمی‌سازد. رتبه‌بندی
/// Bad Actor عمداً یک فرمول ترکیبی مبهم نیست: اول با تعداد خرابی، بعد با Downtime (Tie-Break)
/// مرتب می‌شود - هر دو عامل صریحاً در RankFactors توضیح داده می‌شوند (بند ۲۱: «هیچ فرمول
/// جهانی سخت‌کدشده‌ای نباید باشد»).
/// </summary>
public class ReliabilityAnalyticsService : IReliabilityAnalyticsService
{
    private readonly PEMSDbContext _context;
    private readonly IReliabilityService _reliabilityService;

    public ReliabilityAnalyticsService(PEMSDbContext context, IReliabilityService reliabilityService)
    {
        _context = context;
        _reliabilityService = reliabilityService;
    }

    public async Task<List<EquipmentClassReliabilityDto>> GetClassReliabilityAsync()
    {
        var fleet = await _reliabilityService.GetFleetReliabilityAsync();
        if (fleet.Count == 0) return new List<EquipmentClassReliabilityDto>();

        var equipmentIds = fleet.Select(x => x.EquipmentId).ToList();
        var classByEquipment = await _context.Equipments
            .AsNoTracking()
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .Select(x => new { x.EquipmentId, x.EquipmentClassId })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.EquipmentClassId);

        var populationByClass = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.EquipmentClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var allClasses = await _context.EquipmentClasses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.EquipmentClassId, x.Code, x.Name })
            .ToDictionaryAsync(x => x.EquipmentClassId);

        var grouped = fleet
            .Where(x => classByEquipment.GetValueOrDefault(x.EquipmentId) != null)
            .GroupBy(x => classByEquipment[x.EquipmentId]!.Value);

        var result = new List<EquipmentClassReliabilityDto>();
        foreach (var group in grouped)
        {
            if (!allClasses.TryGetValue(group.Key, out var cls)) continue;

            var mtbfValues = group.Where(x => x.MTBFHours.HasValue).Select(x => x.MTBFHours!.Value).ToList();
            var mttrValues = group.Where(x => x.MTTRHours.HasValue).Select(x => x.MTTRHours!.Value).ToList();
            var availabilityValues = group.Where(x => x.AvailabilityPercent.HasValue).Select(x => x.AvailabilityPercent!.Value).ToList();

            result.Add(new EquipmentClassReliabilityDto
            {
                EquipmentClassId = group.Key,
                Code = cls.Code,
                Name = cls.Name,
                EquipmentPopulation = populationByClass.GetValueOrDefault(group.Key, 0),
                EquipmentWithFailureHistory = group.Count(),
                FailureCount = group.Sum(x => x.FailureCount),
                TotalDowntimeHours = group.Sum(x => x.TotalDowntimeHours),
                AverageMTBFHours = mtbfValues.Count > 0 ? mtbfValues.Average() : null,
                AverageMTTRHours = mttrValues.Count > 0 ? mttrValues.Average() : null,
                AverageAvailabilityPercent = availabilityValues.Count > 0 ? availabilityValues.Average() : null,
                MTBFSampleSize = mtbfValues.Count,
                MTTRSampleSize = mttrValues.Count
            });
        }

        return result.OrderByDescending(x => x.FailureCount).ToList();
    }

    public async Task<EquipmentClassReliabilityDto?> GetClassReliabilityAsync(int equipmentClassId)
    {
        var all = await GetClassReliabilityAsync();
        var found = all.FirstOrDefault(x => x.EquipmentClassId == equipmentClassId);
        if (found != null) return found;

        // کلاسی که هیچ Equipment ای با تاریخچه‌ی خرابی ندارد - همچنان باید در نتیجه دیده شود
        // (طبق بند ۸: عدم وجود داده، نه صفر جعلی)
        var cls = await _context.EquipmentClasses.AsNoTracking().FirstOrDefaultAsync(x => x.EquipmentClassId == equipmentClassId);
        if (cls == null) return null;

        var population = await _context.Equipments.CountAsync(x => x.IsActive && x.EquipmentClassId == equipmentClassId);
        return new EquipmentClassReliabilityDto
        {
            EquipmentClassId = cls.EquipmentClassId,
            Code = cls.Code,
            Name = cls.Name,
            EquipmentPopulation = population
        };
    }

    public async Task<List<FailureParetoItemDto>> GetFailureModeParetoAsync(DateTime? from, DateTime? to, int top = 20)
    {
        var query = _context.FailureRecords.AsNoTracking().Where(x => x.IsActive);
        if (from.HasValue) query = query.Where(x => x.FailureDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.FailureDate <= to.Value);

        var grouped = await query
            .Where(x => x.FailureModeRef != null)
            .GroupBy(x => x.FailureModeRef!.Name)
            .Select(g => new { Key = g.Key, Count = g.Count(), Downtime = g.Sum(x => x.DowntimeHours ?? 0) })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();

        return BuildParetoWithCumulative(grouped.Select(x => (x.Key, x.Count, x.Downtime)));
    }

    public async Task<List<FailureParetoItemDto>> GetFailureCauseParetoAsync(DateTime? from, DateTime? to, int top = 20)
    {
        var query = _context.FailureRecords.AsNoTracking().Where(x => x.IsActive);
        if (from.HasValue) query = query.Where(x => x.FailureDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.FailureDate <= to.Value);

        var grouped = await query
            .Where(x => x.FailureCauseRef != null)
            .GroupBy(x => x.FailureCauseRef!.Name)
            .Select(g => new { Key = g.Key, Count = g.Count(), Downtime = g.Sum(x => x.DowntimeHours ?? 0) })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();

        return BuildParetoWithCumulative(grouped.Select(x => (x.Key, x.Count, x.Downtime)));
    }

    private static List<FailureParetoItemDto> BuildParetoWithCumulative(IEnumerable<(string Key, int Count, decimal Downtime)> items)
    {
        var list = items.ToList();
        var total = list.Sum(x => x.Count);
        var cumulative = 0;
        var result = new List<FailureParetoItemDto>();

        foreach (var item in list)
        {
            cumulative += item.Count;
            result.Add(new FailureParetoItemDto
            {
                Key = item.Key,
                Count = item.Count,
                DowntimeHours = item.Downtime,
                CumulativePercent = total == 0 ? 0 : Math.Round(cumulative * 100.0 / total, 1)
            });
        }

        return result;
    }

    public async Task<List<BadActorDto>> GetBadActorsAsync(DateTime? from, DateTime? to, int top = 20)
    {
        var fleet = await _reliabilityService.GetFleetReliabilityAsync();
        if (fleet.Count == 0) return new List<BadActorDto>();

        // طبق بند ۳۹: بازه‌ی زمانی روی خودِ رکوردهای FailureHistory اعمال می‌شود، نه کل تاریخچه
        var filtered = fleet.Select(eq =>
        {
            var events = eq.FailureHistory.Where(f =>
                (!from.HasValue || f.FailureDate >= from.Value) &&
                (!to.HasValue || f.FailureDate <= to.Value)).ToList();

            return new
            {
                Equipment = eq,
                FailureCount = events.Count,
                DowntimeHours = events.Sum(f => f.DowntimeHours ?? 0)
            };
        })
        .Where(x => x.FailureCount > 0)
        .OrderByDescending(x => x.FailureCount)
        .ThenByDescending(x => x.DowntimeHours)
        .Take(top)
        .ToList();

        if (filtered.Count == 0) return new List<BadActorDto>();

        var equipmentIds = filtered.Select(x => x.Equipment.EquipmentId).ToList();
        var classNames = await _context.Equipments
            .AsNoTracking()
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .Select(x => new { x.EquipmentId, ClassName = x.EquipmentClassRef != null ? x.EquipmentClassRef.Name : null })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.ClassName);

        var criticalityByEquipment = await _context.EquipmentCriticalities
            .AsNoTracking()
            .Where(x => equipmentIds.Contains(x.EquipmentId) && x.IsActive)
            .ToDictionaryAsync(x => x.EquipmentId, x => x.CriticalityClass);

        // طبق «PEMS Phase 7» بند ۱۶: گسترش Bad Actor - فقط برای Top-N از قبل انتخاب‌شده (نه
        // کل ناوگان)، پس هزینه‌ی این Query محدود است.
        var maintenanceCounts = await _context.MaintenanceHistories
            .AsNoTracking()
            .Where(x => x.IsActive && equipmentIds.Contains(x.EquipmentId))
            .GroupBy(x => x.EquipmentId)
            .Select(g => new { EquipmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.Count);

        return filtered.Select(x =>
        {
            // طبق بند ۱۵: همان قاعده‌ی خرابی تکراری (پنجره‌ی ۹۰ روزه) - از روی تاریخچه‌ای که
            // همین‌جا از قبل در حافظه است، بدون Query جدید.
            const int repeatWindowDays = 90;
            var repeatCount = x.Equipment.FailureHistory
                .GroupBy(f => f.FailureMode)
                .Count(g =>
                {
                    var dates = g.Select(f => f.FailureDate).OrderBy(d => d).ToList();
                    for (var i = 1; i < dates.Count; i++)
                        if ((dates[i] - dates[i - 1]).TotalDays <= repeatWindowDays) return true;
                    return false;
                });

            var primaryFailureMode = x.Equipment.FailureHistory
                .Where(f => !string.IsNullOrWhiteSpace(f.FailureMode))
                .GroupBy(f => f.FailureMode)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var primaryFailureCause = x.Equipment.FailureHistory
                .Where(f => !string.IsNullOrWhiteSpace(f.FailureCause))
                .GroupBy(f => f.FailureCause)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            return new BadActorDto
            {
                EquipmentId = x.Equipment.EquipmentId,
                TagNumber = x.Equipment.TagNumber,
                EquipmentClassName = classNames.GetValueOrDefault(x.Equipment.EquipmentId),
                CriticalityClass = criticalityByEquipment.GetValueOrDefault(x.Equipment.EquipmentId),
                FailureCount = x.FailureCount,
                TotalDowntimeHours = x.DowntimeHours,
                MTBFHours = x.Equipment.MTBFHours,
                RankScore = x.FailureCount,
                RankFactors = new List<string>
                {
                    $"Ranked primarily by failure count ({x.FailureCount})",
                    $"Tie-broken by total downtime ({x.DowntimeHours:0.#}h)"
                },
                RepeatFailureCount = repeatCount,
                MTTRHours = x.Equipment.MTTRHours,
                MaintenanceCount = maintenanceCounts.GetValueOrDefault(x.Equipment.EquipmentId, 0),
                PrimaryFailureMode = primaryFailureMode,
                PrimaryFailureCause = primaryFailureCause
            };
        }).ToList();
    }

    public async Task<List<ReliabilityTrendPointDto>> GetMonthlyTrendAsync(DateTime? from, DateTime? to)
    {
        var query = _context.FailureRecords.AsNoTracking().Where(x => x.IsActive);
        if (from.HasValue) query = query.Where(x => x.FailureDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.FailureDate <= to.Value);

        var grouped = await query
            .GroupBy(x => new { x.FailureDate.Year, x.FailureDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count(),
                Downtime = g.Sum(x => x.DowntimeHours ?? 0)
            })
            .ToListAsync();

        return grouped
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new ReliabilityTrendPointDto
            {
                PeriodLabel = $"{x.Year:D4}-{x.Month:D2}",
                PeriodStart = new DateTime(x.Year, x.Month, 1),
                FailureCount = x.Count,
                TotalDowntimeHours = x.Downtime
            })
            .ToList();
    }
}
