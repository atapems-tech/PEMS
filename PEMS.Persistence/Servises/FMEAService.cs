using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 4» بند ۱۳/۲۰/۳۵: پیاده‌سازی چرخه‌عمر FMEA. Approve/Reject هرگز این‌جا
/// بررسی مجوز نمی‌کند - آن مسئولیت Controller است (طبق الگوی رایج پروژه: منطق کسب‌وکار در
/// Persistence، تصمیم دسترسی در Web/Controller). این سرویس فقط تضمین می‌کند که یک تحلیل
/// APPROVED هرگز مستقیم ویرایش نشود.
/// </summary>
public class FMEAService : IFMEAService
{
    private readonly PEMSDbContext _context;

    public FMEAService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<FMEAAnalysisDto>> GetForEquipmentClassAsync(int equipmentClassId)
    {
        var analyses = await _context.FMEAAnalyses
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == equipmentClassId && x.IsActive)
            .Include(x => x.EquipmentClass)
            .OrderByDescending(x => x.Revision)
            .ToListAsync();

        var result = new List<FMEAAnalysisDto>();
        foreach (var a in analyses)
            result.Add(await MapAsync(a));

        return result;
    }

    public async Task<FMEAAnalysisDto?> GetByIdAsync(int fmeaAnalysisId)
    {
        var analysis = await _context.FMEAAnalyses
            .AsNoTracking()
            .Include(x => x.EquipmentClass)
            .FirstOrDefaultAsync(x => x.FMEAAnalysisId == fmeaAnalysisId);

        return analysis == null ? null : await MapAsync(analysis);
    }

    private async Task<FMEAAnalysisDto> MapAsync(FMEAAnalysis a)
    {
        var items = await _context.FMEAItems
            .AsNoTracking()
            .Where(x => x.FMEAAnalysisId == a.FMEAAnalysisId)
            .Include(x => x.FunctionalFailure)
            .Include(x => x.FailureMode)
            .Include(x => x.FailureCause)
            .Include(x => x.FailureMechanism)
            .Include(x => x.FailureEffect)
            .Include(x => x.FailureConsequence)
            .Include(x => x.MaintenanceStrategyType)
            .Include(x => x.StandardReference)
            .ToListAsync();

        return new FMEAAnalysisDto
        {
            FMEAAnalysisId = a.FMEAAnalysisId,
            EquipmentClassId = a.EquipmentClassId,
            EquipmentClassName = a.EquipmentClass?.Name ?? string.Empty,
            Title = a.Title,
            Description = a.Description,
            Revision = a.Revision,
            Status = a.Status,
            AnalysisDate = a.AnalysisDate,
            PreparedBy = a.PreparedBy,
            ReviewedBy = a.ReviewedBy,
            ApprovedBy = a.ApprovedBy,
            ApprovedAt = a.ApprovedAt,
            Items = items.Select(x => new FMEAItemDto
            {
                FMEAItemId = x.FMEAItemId,
                FMEAAnalysisId = x.FMEAAnalysisId,
                FunctionalFailureId = x.FunctionalFailureId,
                FunctionalFailureTitle = x.FunctionalFailure?.Title,
                FailureModeId = x.FailureModeId,
                FailureModeName = x.FailureMode?.Name ?? string.Empty,
                FailureCauseId = x.FailureCauseId,
                FailureCauseName = x.FailureCause?.Name,
                FailureMechanismId = x.FailureMechanismId,
                FailureMechanismName = x.FailureMechanism?.Name,
                FailureEffectId = x.FailureEffectId,
                FailureEffectName = x.FailureEffect?.Name,
                FailureConsequenceId = x.FailureConsequenceId,
                FailureConsequenceName = x.FailureConsequence?.Name,
                DetectionMethod = x.DetectionMethod,
                ExistingControl = x.ExistingControl,
                RecommendedAction = x.RecommendedAction,
                Severity = x.Severity,
                Occurrence = x.Occurrence,
                Detectability = x.Detectability,
                RiskPriorityNumber = x.RiskPriorityNumber,
                MaintenanceStrategyTypeId = x.MaintenanceStrategyTypeId,
                MaintenanceStrategyTypeName = x.MaintenanceStrategyType?.Name,
                StandardReferenceId = x.StandardReferenceId,
                StandardReferenceCode = x.StandardReference?.Code
            }).ToList()
        };
    }

    public async Task<int> CreateDraftAsync(int equipmentClassId, string title, string? description, string preparedBy)
    {
        // طبق بند ۳۵: نسخه‌ی جدید همیشه Revision بعدی از آخرین نسخه‌ی همین کلاس است
        var lastRevision = await _context.FMEAAnalyses
            .Where(x => x.EquipmentClassId == equipmentClassId)
            .Select(x => (int?)x.Revision)
            .MaxAsync() ?? 0;

        var analysis = new FMEAAnalysis
        {
            EquipmentClassId = equipmentClassId,
            Title = title,
            Description = description,
            Revision = lastRevision + 1,
            Status = "DRAFT",
            PreparedBy = preparedBy,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        _context.FMEAAnalyses.Add(analysis);
        await _context.SaveChangesAsync();

        return analysis.FMEAAnalysisId;
    }

    public async Task<int> AddItemAsync(int fmeaAnalysisId, int failureModeId, int? functionalFailureId,
        int? failureCauseId, int? failureMechanismId, int? failureEffectId, int? failureConsequenceId,
        string? detectionMethod, string? existingControl, string? recommendedAction,
        int? severity, int? occurrence, int? detectability,
        int? maintenanceStrategyTypeId, int? standardReferenceId)
    {
        var analysis = await _context.FMEAAnalyses.FirstOrDefaultAsync(x => x.FMEAAnalysisId == fmeaAnalysisId);
        if (analysis == null) throw new InvalidOperationException("FMEA analysis not found.");

        // طبق بند ۳۵: یک تحلیل APPROVED هرگز مستقیم ویرایش نمی‌شود - فقط DRAFT قابل‌افزودن است
        if (analysis.Status != "DRAFT")
            throw new InvalidOperationException($"Cannot add items to an FMEA analysis with status '{analysis.Status}'. Only DRAFT analyses are editable.");

        var item = new FMEAItem
        {
            FMEAAnalysisId = fmeaAnalysisId,
            FunctionalFailureId = functionalFailureId,
            FailureModeId = failureModeId,
            FailureCauseId = failureCauseId,
            FailureMechanismId = failureMechanismId,
            FailureEffectId = failureEffectId,
            FailureConsequenceId = failureConsequenceId,
            DetectionMethod = detectionMethod,
            ExistingControl = existingControl,
            RecommendedAction = recommendedAction,
            Severity = severity,
            Occurrence = occurrence,
            Detectability = detectability,
            MaintenanceStrategyTypeId = maintenanceStrategyTypeId,
            StandardReferenceId = standardReferenceId,
            CreatedDate = DateTime.Now
        };

        _context.FMEAItems.Add(item);
        await _context.SaveChangesAsync();

        return item.FMEAItemId;
    }

    public async Task<bool> SubmitForReviewAsync(int fmeaAnalysisId, string reviewedBy)
    {
        var analysis = await _context.FMEAAnalyses.FirstOrDefaultAsync(x => x.FMEAAnalysisId == fmeaAnalysisId);
        if (analysis == null || analysis.Status != "DRAFT") return false;

        analysis.Status = "UNDER_REVIEW";
        analysis.ReviewedBy = reviewedBy;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveAsync(int fmeaAnalysisId, string approvedBy)
    {
        var analysis = await _context.FMEAAnalyses.FirstOrDefaultAsync(x => x.FMEAAnalysisId == fmeaAnalysisId);
        if (analysis == null || analysis.Status != "UNDER_REVIEW") return false;

        // طبق بند ۳۵: تایید یک نسخه، نسخه‌های تاییدشده‌ی قبلی همین کلاس را SUPERSEDED می‌کند -
        // هرگز حذف نمی‌شوند، فقط علامت‌گذاری می‌شوند.
        var previousApproved = await _context.FMEAAnalyses
            .Where(x => x.EquipmentClassId == analysis.EquipmentClassId && x.Status == "APPROVED")
            .ToListAsync();
        foreach (var prev in previousApproved)
        {
            prev.Status = "SUPERSEDED";
            prev.SupersededByAnalysisId = analysis.FMEAAnalysisId;
        }

        analysis.Status = "APPROVED";
        analysis.ApprovedBy = approvedBy;
        analysis.ApprovedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(int fmeaAnalysisId, string reviewedBy)
    {
        var analysis = await _context.FMEAAnalyses.FirstOrDefaultAsync(x => x.FMEAAnalysisId == fmeaAnalysisId);
        if (analysis == null || analysis.Status != "UNDER_REVIEW") return false;

        analysis.Status = "DRAFT"; // برگشت به DRAFT برای اصلاح - نه حذف
        analysis.ReviewedBy = reviewedBy;
        await _context.SaveChangesAsync();
        return true;
    }
}
