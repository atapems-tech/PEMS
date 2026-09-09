using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 3.5» بند ۱۵-۲۳: پیاده‌سازی ماتریس پوشش. همه‌ی جدول‌های ورودی (EquipmentClass،
/// MaintenanceActivityEquipmentClass، PMTemplate، FailureMode) کوچک هستند (کل ~۳۳۰ ردیف روی هم) -
/// یک‌بار کامل واکشی و در حافظه Group/Resolve می‌شوند؛ فقط شمارش Equipment per Class از GROUP BY
/// واقعی SQL روی ۲۱٬۶۲۵ ردیف می‌آید (نه بارگذاری خودِ ردیف‌ها).
/// </summary>
public class EquipmentClassCoverageService : IEquipmentClassCoverageService
{
    private readonly PEMSDbContext _context;

    public EquipmentClassCoverageService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<MaintenanceIntelligenceCoverageSummaryDto> GetCoverageMatrixAsync()
    {
        var allClasses = await _context.EquipmentClasses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.EquipmentClassId, x.Code, x.Name, x.ParentEquipmentClassId })
            .ToListAsync();

        var parentById = allClasses.ToDictionary(x => x.EquipmentClassId, x => x.ParentEquipmentClassId);
        var nameById = allClasses.ToDictionary(x => x.EquipmentClassId, x => x.Name);

        // طبق بند ۱۵: تعداد Equipment مستقیماً روی همین کلاس دقیق (نه Sum روی زیرمجموعه‌ها) -
        // GROUP BY واقعی SQL، نه بارگذاری ۲۱٬۶۲۵ ردیف.
        var equipmentCountByClass = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.EquipmentClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var activityLinks = await _context.MaintenanceActivityEquipmentClasses
            .AsNoTracking()
            .Where(x => x.MaintenanceActivity!.IsActive)
            .Select(x => new { x.EquipmentClassId, x.MaintenanceActivityId })
            .ToListAsync();
        var activitiesByClass = activityLinks.GroupBy(x => x.EquipmentClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.MaintenanceActivityId).ToHashSet());

        var templates = await _context.PMTemplates
            .AsNoTracking()
            .Where(x => x.EquipmentClassId != null && x.IsActive)
            .Select(x => new { EquipmentClassId = x.EquipmentClassId!.Value, x.PMTemplateId })
            .ToListAsync();
        var templatesByClass = templates.GroupBy(x => x.EquipmentClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PMTemplateId).ToHashSet());

        var jobPlans = await _context.JobPlans
            .AsNoTracking()
            .Where(x => x.EquipmentClassId != null && x.IsActive)
            .Select(x => new { EquipmentClassId = x.EquipmentClassId!.Value, x.JobPlanId })
            .ToListAsync();
        var jobPlansByClass = jobPlans.GroupBy(x => x.EquipmentClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.JobPlanId).ToHashSet());

        var failureModes = await _context.FailureModes
            .AsNoTracking()
            .Where(x => x.EquipmentClassId != null && x.IsActive)
            .Select(x => new { EquipmentClassId = x.EquipmentClassId!.Value, x.FailureModeId })
            .ToListAsync();
        var failureModesByClass = failureModes.GroupBy(x => x.EquipmentClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.FailureModeId).ToHashSet());

        var allActivityIds = activityLinks.Select(x => x.MaintenanceActivityId).Distinct().ToList();
        var standardsByActivity = (await _context.Set<MaintenanceActivityStandardReference>()
                .AsNoTracking()
                .Where(x => allActivityIds.Contains(x.MaintenanceActivityId))
                .Select(x => new { x.MaintenanceActivityId, x.StandardReferenceId })
                .ToListAsync())
            .GroupBy(x => x.MaintenanceActivityId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StandardReferenceId).ToHashSet());

        var strategyTypesByActivity = (await _context.Set<MaintenanceActivityStrategyType>()
                .AsNoTracking()
                .Where(x => allActivityIds.Contains(x.MaintenanceActivityId))
                .Select(x => new { x.MaintenanceActivityId, x.MaintenanceStrategyTypeId })
                .ToListAsync())
            .GroupBy(x => x.MaintenanceActivityId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.MaintenanceStrategyTypeId).ToHashSet());

        var result = new MaintenanceIntelligenceCoverageSummaryDto();

        foreach (var cls in allClasses)
        {
            var classId = cls.EquipmentClassId;

            // زنجیره‌ی سلسله‌مراتب - همان الگوریتم MaintenanceIntelligenceService (بند ۱۸)
            var chain = new List<int>();
            int? current = classId;
            var guard = 0;
            while (current.HasValue && guard++ < 10 && !chain.Contains(current.Value))
            {
                chain.Add(current.Value);
                current = parentById.TryGetValue(current.Value, out var p) ? p : null;
            }
            var ancestors = chain.Skip(1).ToList(); // بدون خودِ کلاس

            var directActivities = activitiesByClass.GetValueOrDefault(classId, new HashSet<int>());
            var inheritedActivities = ancestors.SelectMany(a => activitiesByClass.GetValueOrDefault(a, new HashSet<int>()))
                .Where(id => !directActivities.Contains(id)).Distinct().ToList();

            var directTemplates = templatesByClass.GetValueOrDefault(classId, new HashSet<int>());
            var inheritedTemplates = ancestors.SelectMany(a => templatesByClass.GetValueOrDefault(a, new HashSet<int>()))
                .Where(id => !directTemplates.Contains(id)).Distinct().ToList();

            var directJobPlans = jobPlansByClass.GetValueOrDefault(classId, new HashSet<int>());
            var inheritedJobPlans = ancestors.SelectMany(a => jobPlansByClass.GetValueOrDefault(a, new HashSet<int>()))
                .Where(id => !directJobPlans.Contains(id)).Distinct().ToList();

            var directFailureModes = failureModesByClass.GetValueOrDefault(classId, new HashSet<int>());
            var inheritedFailureModes = ancestors.SelectMany(a => failureModesByClass.GetValueOrDefault(a, new HashSet<int>()))
                .Where(id => !directFailureModes.Contains(id)).Distinct().ToList();

            var totalActivityIds = directActivities.Union(inheritedActivities).ToList();
            var allStandardIds = totalActivityIds.SelectMany(id => standardsByActivity.GetValueOrDefault(id, new HashSet<int>())).Distinct().ToList();
            var directStandardIds = directActivities.SelectMany(id => standardsByActivity.GetValueOrDefault(id, new HashSet<int>())).Distinct().ToList();
            var inheritedStandardIds = allStandardIds.Except(directStandardIds).ToList();

            var strategyTypeIds = totalActivityIds.SelectMany(id => strategyTypesByActivity.GetValueOrDefault(id, new HashSet<int>())).Distinct().ToList();

            var equipmentCount = equipmentCountByClass.GetValueOrDefault(classId, 0);

            var dto = new EquipmentClassCoverageDto
            {
                EquipmentClassId = classId,
                Code = cls.Code,
                Name = cls.Name,
                ParentClassName = cls.ParentEquipmentClassId.HasValue ? nameById.GetValueOrDefault(cls.ParentEquipmentClassId.Value) : null,
                EquipmentCount = equipmentCount,
                DirectActivityCount = directActivities.Count,
                InheritedActivityCount = inheritedActivities.Count,
                DirectPMTemplateCount = directTemplates.Count,
                InheritedPMTemplateCount = inheritedTemplates.Count,
                DirectJobPlanCount = directJobPlans.Count,
                InheritedJobPlanCount = inheritedJobPlans.Count,
                DirectFailureModeCount = directFailureModes.Count,
                InheritedFailureModeCount = inheritedFailureModes.Count,
                DirectStandardReferenceCount = directStandardIds.Count,
                InheritedStandardReferenceCount = inheritedStandardIds.Count,
                StrategyTypeCount = strategyTypeIds.Count
            };

            // طبق بند ۱۶: طبقه‌بندی خلأ - هرگز خودکار پر نمی‌شود، فقط توضیح داده می‌شود
            if (equipmentCount > 0 && dto.TotalActivityCoverage == 0)
                dto.GapReasons.Add("Missing applicability: equipment exists on this class but no Maintenance Activity is mapped (direct or inherited).");
            if (equipmentCount > 0 && dto.TotalPMTemplateCoverage == 0)
                dto.GapReasons.Add("Missing PM template: equipment exists but no PMTemplate covers this class (direct or inherited).");
            if (equipmentCount > 0 && dto.TotalFailureModeCoverage == 0)
                dto.GapReasons.Add("Missing failure taxonomy: equipment exists but no FailureMode is defined for this class (direct or inherited).");
            if (equipmentCount == 0 && dto.TotalActivityCoverage == 0 && dto.TotalPMTemplateCoverage == 0 && dto.TotalFailureModeCoverage == 0)
                dto.GapReasons.Add("Intentional: no equipment currently assigned to this class and no master data defined yet.");

            result.Classes.Add(dto);
        }

        result.Classes = result.Classes.OrderByDescending(x => x.EquipmentCount).ThenBy(x => x.Name).ToList();

        result.ClassesWithEquipment = result.Classes.Count(x => x.EquipmentCount > 0);
        result.ClassesWithDirectOrInheritedActivities = result.Classes.Count(x => x.TotalActivityCoverage > 0);
        result.ClassesWithDirectOrInheritedPMTemplates = result.Classes.Count(x => x.TotalPMTemplateCoverage > 0);
        result.ClassesWithDirectOrInheritedFailureModes = result.Classes.Count(x => x.TotalFailureModeCoverage > 0);
        result.ClassesWithDirectOrInheritedStandards = result.Classes.Count(x => (x.DirectStandardReferenceCount + x.InheritedStandardReferenceCount) > 0);
        result.ClassesWithJobPlans = result.Classes.Count(x => (x.DirectJobPlanCount + x.InheritedJobPlanCount) > 0);

        // طبق بند ۲۳: پوشش فقط روی کلاس‌هایی محاسبه می‌شود که واقعاً Equipment دارند - چون
        // کلاس‌های بدون تجهیز اصلاً کاندید هوش نگهداری در عمل نیستند.
        var classesWithEquipment = result.Classes.Where(x => x.EquipmentCount > 0).ToList();
        var denom = classesWithEquipment.Count;
        result.ActivityCoveragePercent = denom == 0 ? 0 : Math.Round(classesWithEquipment.Count(x => x.TotalActivityCoverage > 0) * 100.0 / denom, 1);
        result.PMTemplateCoveragePercent = denom == 0 ? 0 : Math.Round(classesWithEquipment.Count(x => x.TotalPMTemplateCoverage > 0) * 100.0 / denom, 1);
        result.FailureModeCoveragePercent = denom == 0 ? 0 : Math.Round(classesWithEquipment.Count(x => x.TotalFailureModeCoverage > 0) * 100.0 / denom, 1);
        result.StandardsCoveragePercent = denom == 0 ? 0 : Math.Round(classesWithEquipment.Count(x => (x.DirectStandardReferenceCount + x.InheritedStandardReferenceCount) > 0) * 100.0 / denom, 1);

        return result;
    }
}
