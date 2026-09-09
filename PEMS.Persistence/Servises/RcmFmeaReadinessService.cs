using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 4» بند ۲۶-۲۸: موتور آماده‌بودی FMEA/RCM. آستانه‌ها (Thresholds) عمداً در
/// یک‌جا و با توضیح صریح نگه داشته شده‌اند (بند ۲۶: «باید مبتنی‌بر شواهد و قابل‌تنظیم باشند،
/// نه یک عدد دلبخواهی که ادعا شود استاندارد صنعتی است») - این‌ها قرارداد پروژه‌ی PEMS هستند،
/// نه یک الزام رسمی ISO/SAE.
///
/// FMEA Readiness (بند ۲۷): آیا داده‌ی کافی برای *شروع* یک FMEA معتبر وجود دارد؟
///   NOT_APPLICABLE    → هیچ Equipment واقعی روی این کلاس نیست
///   INSUFFICIENT_DATA → هیچ FailureMode ای تعریف نشده (نمی‌توان اصلاً شروع کرد)
///   PARTIALLY_READY   → FailureMode هست، ولی حداقل یکی از ابعاد کلیدی (Functional
///                       Requirement/Cause/Effect/Consequence) غایب است
///   READY             → همه‌ی ابعاد کلیدی حداقل یک رکورد دارند
///
/// RCM Readiness (بند ۲۸): علاوه بر FMEA، آیا زیرساخت تصمیم‌گیری هم موجود است؟
///   NOT_APPLICABLE    → هیچ Equipment واقعی نیست
///   INSUFFICIENT_DATA → هیچ FMEAAnalysis تاییدشده‌ای برای این کلاس وجود ندارد
///   PARTIALLY_READY   → FMEA تاییدشده هست، ولی یا Strategy Type ای موجود نیست یا هنوز هیچ
///                       RCMAnalysis تاییدشده‌ای ساخته نشده
///   READY             → FMEA تاییدشده + Strategy Type موجود + حداقل یک RCMAnalysis تاییدشده
/// </summary>
public class RcmFmeaReadinessService : IRcmFmeaReadinessService
{
    private readonly PEMSDbContext _context;

    public RcmFmeaReadinessService(PEMSDbContext context)
    {
        _context = context;
    }

    private sealed class FailureModeProjection
    {
        public int EquipmentClassId { get; set; }
        public int FailureModeId { get; set; }
        public int? FailureEffectId { get; set; }
        public int? FailureConsequenceId { get; set; }
    }

    private sealed class AnalysisStats
    {
        public int Approved { get; set; }
        public int Draft { get; set; }
    }

    public async Task<RcmFmeaCoverageMatrixDto> GetCoverageMatrixAsync()
    {
        var allClasses = await _context.EquipmentClasses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.EquipmentClassId, x.Code, x.Name })
            .ToListAsync();

        var equipmentCountByClass = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.EquipmentClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var frCountByClass = await _context.FunctionalRequirements
            .Where(x => x.IsActive)
            .GroupBy(x => x.EquipmentClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var ffCountByClass = await _context.FunctionalFailures
            .Where(x => x.IsActive)
            .GroupBy(x => x.FunctionalRequirement!.EquipmentClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var failureModes = await _context.FailureModes
            .AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .Select(x => new FailureModeProjection
            {
                EquipmentClassId = x.EquipmentClassId!.Value,
                FailureModeId = x.FailureModeId,
                FailureEffectId = x.FailureEffectId,
                FailureConsequenceId = x.FailureConsequenceId
            })
            .ToListAsync();

        var modeCauseLinks = await _context.FailureModeCauses.AsNoTracking().ToListAsync();
        var causeMechanismLinks = await _context.FailureCauseMechanisms.AsNoTracking().ToListAsync();

        var activityCountByClass = await _context.MaintenanceActivityEquipmentClasses
            .Where(x => x.MaintenanceActivity!.IsActive)
            .GroupBy(x => x.EquipmentClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var pmTemplateCountByClass = await _context.PMTemplates
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.EquipmentClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var jobPlanCountByClass = await _context.JobPlans
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.EquipmentClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var strategyTypeTotal = await _context.MaintenanceStrategyTypes.CountAsync(x => x.IsActive);

        var fmeaByClass = await _context.FMEAAnalyses
            .Where(x => x.IsActive)
            .GroupBy(x => x.EquipmentClassId)
            .Select(g => new
            {
                ClassId = g.Key,
                Approved = g.Count(x => x.Status == "APPROVED"),
                Draft = g.Count(x => x.Status == "DRAFT" || x.Status == "UNDER_REVIEW")
            })
            .ToDictionaryAsync(x => x.ClassId, x => new AnalysisStats { Approved = x.Approved, Draft = x.Draft });

        var rcmByClass = await _context.RCMAnalyses
            .Where(x => x.IsActive)
            .GroupBy(x => x.EquipmentClassId)
            .Select(g => new
            {
                ClassId = g.Key,
                Approved = g.Count(x => x.Status == "APPROVED"),
                Draft = g.Count(x => x.Status == "DRAFT" || x.Status == "UNDER_REVIEW")
            })
            .ToDictionaryAsync(x => x.ClassId, x => new AnalysisStats { Approved = x.Approved, Draft = x.Draft });

        var result = new RcmFmeaCoverageMatrixDto();

        foreach (var cls in allClasses)
        {
            var dto = BuildReadiness(cls.EquipmentClassId, cls.Code, cls.Name,
                equipmentCountByClass, frCountByClass, ffCountByClass, failureModes,
                modeCauseLinks, causeMechanismLinks, activityCountByClass, pmTemplateCountByClass,
                jobPlanCountByClass, strategyTypeTotal, fmeaByClass, rcmByClass);

            result.Classes.Add(dto);
        }

        result.Classes = result.Classes.OrderByDescending(x => x.EquipmentCount).ThenBy(x => x.Name).ToList();
        result.ClassesWithEquipment = result.Classes.Count(x => x.EquipmentCount > 0);
        result.ClassesFmeaReady = result.Classes.Count(x => x.FMEAReadiness == "READY");
        result.ClassesFmeaPartiallyReady = result.Classes.Count(x => x.FMEAReadiness == "PARTIALLY_READY");
        result.ClassesRcmReady = result.Classes.Count(x => x.RCMReadiness == "READY");
        result.ClassesRcmPartiallyReady = result.Classes.Count(x => x.RCMReadiness == "PARTIALLY_READY");
        result.TotalApprovedFMEAs = fmeaByClass.Values.Sum(x => x.Approved);
        result.TotalDraftFMEAs = fmeaByClass.Values.Sum(x => x.Draft);
        result.TotalApprovedRCMs = rcmByClass.Values.Sum(x => x.Approved);
        result.TotalDraftRCMs = rcmByClass.Values.Sum(x => x.Draft);

        return result;
    }

    public async Task<EquipmentClassRcmFmeaReadinessDto?> GetForEquipmentClassAsync(int equipmentClassId)
    {
        var matrix = await GetCoverageMatrixAsync();
        return matrix.Classes.FirstOrDefault(x => x.EquipmentClassId == equipmentClassId);
    }

    private static EquipmentClassRcmFmeaReadinessDto BuildReadiness(
        int classId, string code, string name,
        Dictionary<int, int> equipmentCountByClass,
        Dictionary<int, int> frCountByClass,
        Dictionary<int, int> ffCountByClass,
        List<FailureModeProjection> failureModes,
        List<FailureModeCause> modeCauseLinks,
        List<FailureCauseMechanism> causeMechanismLinks,
        Dictionary<int, int> activityCountByClass,
        Dictionary<int, int> pmTemplateCountByClass,
        Dictionary<int, int> jobPlanCountByClass,
        int strategyTypeTotal,
        Dictionary<int, AnalysisStats> fmeaByClass,
        Dictionary<int, AnalysisStats> rcmByClass)
    {
        var classModes = failureModes.Where(x => x.EquipmentClassId == classId).ToList();
        var failureModeIds = classModes.Select(x => x.FailureModeId).ToHashSet();

        var causeIds = modeCauseLinks.Where(x => failureModeIds.Contains(x.FailureModeId)).Select(x => x.FailureCauseId).Distinct().ToHashSet();
        var mechanismIds = causeMechanismLinks.Where(x => causeIds.Contains(x.FailureCauseId)).Select(x => x.FailureMechanismId).Distinct().ToHashSet();
        var effectIds = classModes.Where(x => x.FailureEffectId != null).Select(x => x.FailureEffectId!.Value).Distinct().ToHashSet();
        var consequenceIds = classModes.Where(x => x.FailureConsequenceId != null).Select(x => x.FailureConsequenceId!.Value).Distinct().ToHashSet();

        var equipmentCount = equipmentCountByClass.GetValueOrDefault(classId, 0);
        var frCount = frCountByClass.GetValueOrDefault(classId, 0);
        var ffCount = ffCountByClass.GetValueOrDefault(classId, 0);
        var strategyTypeCount = activityCountByClass.ContainsKey(classId) ? strategyTypeTotal : 0; // فقط اگر Activity ای باشد که به Strategy Type اشاره کند معنا دارد

        fmeaByClass.TryGetValue(classId, out var fmeaStats);
        rcmByClass.TryGetValue(classId, out var rcmStats);
        int approvedFmea = fmeaStats?.Approved ?? 0;
        int draftFmea = fmeaStats?.Draft ?? 0;
        int approvedRcm = rcmStats?.Approved ?? 0;
        int draftRcm = rcmStats?.Draft ?? 0;

        var dto = new EquipmentClassRcmFmeaReadinessDto
        {
            EquipmentClassId = classId,
            Code = code,
            Name = name,
            EquipmentCount = equipmentCount,
            FunctionalRequirementCount = frCount,
            FunctionalFailureCount = ffCount,
            FailureModeCount = failureModeIds.Count,
            FailureCauseCount = causeIds.Count,
            FailureMechanismCount = mechanismIds.Count,
            FailureEffectCount = effectIds.Count,
            FailureConsequenceCount = consequenceIds.Count,
            StandardReferenceCount = 0, // طبق طراحی: از طریق Activity ها می‌آید - در Coverage Matrix فاز ۳.۵ موجود است
            MaintenanceActivityCount = activityCountByClass.GetValueOrDefault(classId, 0),
            StrategyTypeCount = strategyTypeCount,
            PMTemplateCount = pmTemplateCountByClass.GetValueOrDefault(classId, 0),
            JobPlanCount = jobPlanCountByClass.GetValueOrDefault(classId, 0),
            ApprovedFMEACount = approvedFmea,
            DraftFMEACount = draftFmea,
            ApprovedRCMCount = approvedRcm,
            DraftRCMCount = draftRcm
        };

        // ==================== FMEA Readiness (بند ۲۷) ====================
        if (equipmentCount == 0)
        {
            dto.FMEAReadiness = "NOT_APPLICABLE";
        }
        else if (dto.FailureModeCount == 0)
        {
            dto.FMEAReadiness = "INSUFFICIENT_DATA";
            dto.MissingDimensions.Add("No FailureMode defined for this class");
        }
        else
        {
            var missing = new List<string>();
            if (frCount == 0) missing.Add("No FunctionalRequirement defined");
            if (dto.FailureCauseCount == 0) missing.Add("No FailureCause linked to this class's Failure Modes");
            if (dto.FailureEffectCount == 0) missing.Add("No FailureEffect linked");
            if (dto.FailureConsequenceCount == 0) missing.Add("No FailureConsequence linked");

            dto.MissingDimensions.AddRange(missing);
            dto.FMEAReadiness = missing.Count == 0 ? "READY" : "PARTIALLY_READY";
        }

        // ==================== RCM Readiness (بند ۲۸) ====================
        if (equipmentCount == 0)
        {
            dto.RCMReadiness = "NOT_APPLICABLE";
        }
        else if (approvedFmea == 0)
        {
            dto.RCMReadiness = "INSUFFICIENT_DATA";
            dto.MissingDimensions.Add("No APPROVED FMEA analysis exists yet - RCM decisions require an approved FMEA foundation");
        }
        else if (dto.StrategyTypeCount == 0)
        {
            dto.RCMReadiness = "PARTIALLY_READY";
            dto.MissingDimensions.Add("No Maintenance Strategy Type context available for decision-making");
        }
        else
        {
            dto.RCMReadiness = approvedRcm > 0 ? "READY" : "PARTIALLY_READY";
            if (approvedRcm == 0)
                dto.MissingDimensions.Add("FMEA approved and strategy types available, but no RCM analysis has been approved yet");
        }

        return dto;
    }
}
