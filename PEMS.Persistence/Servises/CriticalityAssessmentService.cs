using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 7.6»: پیاده‌سازی گردش‌کار ارزیابی بحرانی‌بودن - عیناً الگوی FMEAService
/// (فاز ۴) با تطبیق سطح-Equipment (نه سطح-EquipmentClass). Approve تنها لحظه‌ای است که یک
/// رکورد EquipmentCriticality واقعی منتشر می‌شود - هرگز خودکار در جای دیگر.
/// </summary>
public class CriticalityAssessmentService : ICriticalityAssessmentService
{
    private readonly PEMSDbContext _context;

    // طبق بند ۷: نگاشت صریح و مستندشده A→High, B→Medium, C→Low - همان سه مقداری که
    // EquipmentCriticality.RiskLevel از قبل به‌صورت متن آزاد می‌پذیرفت.
    private static readonly Dictionary<string, string> RiskLevelByClass = new()
    {
        ["A"] = "High",
        ["B"] = "Medium",
        ["C"] = "Low"
    };

    public CriticalityAssessmentService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<CriticalityAssessmentListDto>> GetQueueAsync(
        string? status = null, int? equipmentClassId = null, int page = 1, int pageSize = 50)
    {
        var query = _context.CriticalityAssessments.AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (equipmentClassId.HasValue) query = query.Where(x => x.Equipment!.EquipmentClassId == equipmentClassId.Value);

        var totalCount = await query.CountAsync();
        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CriticalityAssessmentListDto
            {
                CriticalityAssessmentId = x.CriticalityAssessmentId,
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = x.Equipment!.TagNumber,
                EquipmentClassName = x.Equipment.EquipmentClassRef != null ? x.Equipment.EquipmentClassRef.Name : null,
                FrameworkVersion = x.FrameworkVersion,
                Status = x.Status,
                ProposedCriticalityClass = x.ProposedCriticalityClass,
                PreparedBy = x.PreparedBy,
                ApprovedBy = x.ApprovedBy,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return new PagedResult<CriticalityAssessmentListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<CriticalityAssessmentDetailDto?> GetByIdAsync(int assessmentId)
    {
        var assessment = await _context.CriticalityAssessments.AsNoTracking()
            .Include(x => x.Equipment).ThenInclude(e => e!.EquipmentClassRef)
            .Include(x => x.Evidence)
            .FirstOrDefaultAsync(x => x.CriticalityAssessmentId == assessmentId);

        if (assessment == null) return null;

        // طبق بند ۱۳: زنجیره‌ی کامل تاریخچه - همه‌ی نسخه‌های قبلی همین Equipment (هرگز حذف نشده‌اند)
        var priorAssessments = await _context.CriticalityAssessments.AsNoTracking()
            .Where(x => x.EquipmentId == assessment.EquipmentId && x.CriticalityAssessmentId != assessmentId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new CriticalityAssessmentListDto
            {
                CriticalityAssessmentId = x.CriticalityAssessmentId,
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = assessment.Equipment!.TagNumber,
                FrameworkVersion = x.FrameworkVersion,
                Status = x.Status,
                ProposedCriticalityClass = x.ProposedCriticalityClass,
                PreparedBy = x.PreparedBy,
                ApprovedBy = x.ApprovedBy,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return new CriticalityAssessmentDetailDto
        {
            CriticalityAssessmentId = assessment.CriticalityAssessmentId,
            EquipmentId = assessment.EquipmentId,
            EquipmentTagNumber = assessment.Equipment!.TagNumber,
            EquipmentClassName = assessment.Equipment.EquipmentClassRef?.Name,
            FrameworkVersion = assessment.FrameworkVersion,
            Status = assessment.Status,
            AssessmentDate = assessment.AssessmentDate,
            PreparedBy = assessment.PreparedBy,
            ReviewedBy = assessment.ReviewedBy,
            ApprovedBy = assessment.ApprovedBy,
            ApprovedAt = assessment.ApprovedAt,
            ReasonForChange = assessment.ReasonForChange,
            SafetyScore = assessment.SafetyScore,
            ProductionScore = assessment.ProductionScore,
            EnvironmentScore = assessment.EnvironmentScore,
            CalculatedMaxScore = assessment.CalculatedMaxScore,
            ProposedCriticalityClass = assessment.ProposedCriticalityClass,
            Rationale = assessment.Rationale,
            Evidence = assessment.Evidence.Select(e => new CriticalityAssessmentEvidenceDto
            {
                CriticalityAssessmentEvidenceId = e.CriticalityAssessmentEvidenceId,
                SourceType = e.SourceType,
                SourceIdentifier = e.SourceIdentifier,
                Reference = e.Reference,
                Description = e.Description,
                CreatedBy = e.CreatedBy,
                CreatedDate = e.CreatedDate
            }).OrderBy(e => e.CreatedDate).ToList(),
            SupersededByAssessmentId = assessment.SupersededByAssessmentId,
            PriorAssessments = priorAssessments
        };
    }

    public async Task<CriticalityDashboardDto> GetDashboardAsync()
    {
        var dto = new CriticalityDashboardDto
        {
            TotalActiveEquipment = await _context.Equipments.CountAsync(x => x.IsActive),
            FrameworkVersion = CriticalityFramework.CurrentVersion
        };

        var statusCounts = await _context.CriticalityAssessments.AsNoTracking()
            .Where(x => x.IsActive)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        dto.Draft = statusCounts.GetValueOrDefault("DRAFT", 0);
        dto.UnderReview = statusCounts.GetValueOrDefault("UNDER_REVIEW", 0);
        dto.Approved = statusCounts.GetValueOrDefault("APPROVED", 0);
        dto.Superseded = statusCounts.GetValueOrDefault("SUPERSEDED", 0);

        // طبق بند ۲۱-الف: Assessed یعنی تجهیزی که حداقل یک ارزیابی (هر وضعیتی) واقعاً برایش
        // شروع شده - نه فقط APPROVED.
        dto.Assessed = await _context.CriticalityAssessments.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.EquipmentId)
            .Distinct()
            .CountAsync();

        var publishedByClass = await _context.EquipmentCriticalities.AsNoTracking()
            .Where(x => x.IsActive)
            .GroupBy(x => x.CriticalityClass)
            .Select(g => new { Class = g.Key, Count = g.Count() })
            .ToListAsync();

        dto.PublishedCriticalityA = publishedByClass.FirstOrDefault(x => x.Class == "A")?.Count ?? 0;
        dto.PublishedCriticalityB = publishedByClass.FirstOrDefault(x => x.Class == "B")?.Count ?? 0;
        dto.PublishedCriticalityC = publishedByClass.FirstOrDefault(x => x.Class == "C")?.Count ?? 0;
        dto.PublishedCriticalityOther = publishedByClass.Where(x => x.Class != "A" && x.Class != "B" && x.Class != "C").Sum(x => x.Count);

        return dto;
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateDraftAsync(int equipmentId, string? preparedBy, string? rationale)
    {
        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive);
        if (equipment == null) return (false, "تجهیز یافت نشد یا فعال نیست.", null);

        // طبق بند ۱۱: هرگز دو ارزیابی باز (DRAFT/UNDER_REVIEW) هم‌زمان برای یک تجهیز
        var hasOpenAssessment = await _context.CriticalityAssessments
            .AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive && (x.Status == "DRAFT" || x.Status == "UNDER_REVIEW"));
        if (hasOpenAssessment)
            return (false, "یک ارزیابی باز (DRAFT/UNDER_REVIEW) از قبل برای این تجهیز وجود دارد.", null);

        var assessment = new CriticalityAssessment
        {
            EquipmentId = equipmentId,
            FrameworkVersion = CriticalityFramework.CurrentVersion,
            Status = "DRAFT",
            PreparedBy = preparedBy,
            Rationale = rationale,
            IsActive = true,
            CreatedDate = DateTime.Now,
            CreatedBy = preparedBy
        };

        await _context.CriticalityAssessments.AddAsync(assessment);
        await _context.SaveChangesAsync();

        return (true, null, assessment.CriticalityAssessmentId);
    }

    public async Task<(bool Success, string? Error)> SetDimensionScoresAsync(
        int assessmentId, int? safetyScore, int? productionScore, int? environmentScore, string? rationale)
    {
        var assessment = await _context.CriticalityAssessments.FirstOrDefaultAsync(x => x.CriticalityAssessmentId == assessmentId);
        if (assessment == null) return (false, "ارزیابی یافت نشد.");
        // طبق الگوی FMEAAnalysis: فقط DRAFT قابل‌ویرایش است - APPROVED هرگز مستقیم ویرایش نمی‌شود.
        if (assessment.Status != "DRAFT") return (false, $"فقط ارزیابی در وضعیت DRAFT قابل‌ویرایش است (وضعیت فعلی: {assessment.Status}).");

        foreach (var score in new[] { safetyScore, productionScore, environmentScore })
        {
            if (score.HasValue && (score.Value < CriticalityFramework.MinScore || score.Value > CriticalityFramework.MaxScore))
                return (false, $"امتیاز هر بُعد باید بین {CriticalityFramework.MinScore} تا {CriticalityFramework.MaxScore} باشد.");
        }

        assessment.SafetyScore = safetyScore;
        assessment.ProductionScore = productionScore;
        assessment.EnvironmentScore = environmentScore;
        assessment.Rationale = rationale;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddEvidenceAsync(
        int assessmentId, string sourceType, string? sourceIdentifier, string? reference, string description, string? createdBy)
    {
        var assessment = await _context.CriticalityAssessments.FirstOrDefaultAsync(x => x.CriticalityAssessmentId == assessmentId);
        if (assessment == null) return (false, "ارزیابی یافت نشد.");
        if (assessment.Status is "APPROVED" or "SUPERSEDED")
            return (false, $"نمی‌توان به ارزیابی در وضعیت {assessment.Status} شاهد اضافه کرد.");

        if (string.IsNullOrWhiteSpace(description)) return (false, "توضیح شاهد الزامی است.");

        await _context.CriticalityAssessmentEvidences.AddAsync(new CriticalityAssessmentEvidence
        {
            CriticalityAssessmentId = assessmentId,
            SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Other" : sourceType,
            SourceIdentifier = sourceIdentifier,
            Reference = reference,
            Description = description.Trim(),
            CreatedBy = createdBy,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SubmitForReviewAsync(int assessmentId, string? reviewedBy)
    {
        var assessment = await _context.CriticalityAssessments.FirstOrDefaultAsync(x => x.CriticalityAssessmentId == assessmentId);
        if (assessment == null) return (false, "ارزیابی یافت نشد.");
        if (assessment.Status != "DRAFT") return (false, $"فقط ارزیابی DRAFT قابل‌ارسال برای بازبینی است (وضعیت فعلی: {assessment.Status}).");

        // طبق بند ۱۲: حداقل یک شاهد الزامی است - هرگز بدون شاهد به بازبینی نمی‌رود
        if (assessment.SafetyScore == null || assessment.ProductionScore == null || assessment.EnvironmentScore == null)
            return (false, "همه‌ی سه بُعد (ایمنی، تولید، محیط‌زیست) باید قبل از ارسال برای بازبینی امتیازدهی شوند.");

        var hasEvidence = await _context.CriticalityAssessmentEvidences.AnyAsync(x => x.CriticalityAssessmentId == assessmentId);
        if (!hasEvidence) return (false, "حداقل یک شاهد (Evidence) باید قبل از ارسال برای بازبینی ثبت شود.");

        assessment.Status = "UNDER_REVIEW";
        assessment.ReviewedBy = reviewedBy;
        assessment.AssessmentDate ??= DateTime.Now;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(int assessmentId, string? approvedBy)
    {
        var assessment = await _context.CriticalityAssessments
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.CriticalityAssessmentId == assessmentId);
        if (assessment == null) return (false, "ارزیابی یافت نشد.");
        if (assessment.Status != "UNDER_REVIEW") return (false, $"فقط ارزیابی UNDER_REVIEW قابل‌تایید است (وضعیت فعلی: {assessment.Status}).");

        // طبق بند ۲۵: کنترل تکمیلیِ تفکیک وظایف - سازنده نمی‌تواند ارزیابی خودش را تایید کند
        // (کنترل اصلی، همان‌طور که در سراسر این پروژه تثبیت شده، تفکیک نقش/مجوز Approve در
        // سطح Controller است - این‌جا فقط یک محافظ دفاع‌در‌عمق اضافه است).
        if (!string.IsNullOrWhiteSpace(approvedBy) && string.Equals(assessment.PreparedBy, approvedBy, StringComparison.OrdinalIgnoreCase))
            return (false, "سازنده‌ی ارزیابی نمی‌تواند تایید‌کننده‌ی همان ارزیابی باشد (تفکیک وظایف).");

        var proposedClass = assessment.ProposedCriticalityClass;
        if (proposedClass == null)
            return (false, "نتیجه‌ی محاسبه‌شده در دسترس نیست - هر سه بُعد باید امتیازدهی شده باشند.");

        // طبق الگوی FMEAService: تایید یک نسخه، نسخه‌های تاییدشده‌ی قبلی همین تجهیز را
        // SUPERSEDED می‌کند - هرگز حذف نمی‌شوند.
        var previousApproved = await _context.CriticalityAssessments
            .Where(x => x.EquipmentId == assessment.EquipmentId && x.Status == "APPROVED")
            .ToListAsync();
        foreach (var prev in previousApproved)
        {
            prev.Status = "SUPERSEDED";
            prev.SupersededByAssessmentId = assessment.CriticalityAssessmentId;
        }

        assessment.Status = "APPROVED";
        assessment.ApprovedBy = approvedBy;
        assessment.ApprovedAt = DateTime.Now;

        // ==================== انتشار در EquipmentCriticality موجود ====================
        // طبق بند ۱۰: دقیقاً همان الگوی RcmOperationalBridgeService فاز ۶.۱ - توصیه‌ی تاییدشده
        // در جدول عملیاتیِ از قبل موجود منتشر می‌شود؛ رکورد فعالِ قبلی غیرفعال (نه حذف) می‌شود -
        // دقیقاً همان رفتاری که EquipmentCriticalityController.Assess از قبل دارد.
        var existingCriticality = await _context.EquipmentCriticalities
            .FirstOrDefaultAsync(x => x.EquipmentId == assessment.EquipmentId && x.IsActive);
        if (existingCriticality != null)
            existingCriticality.IsActive = false;

        await _context.EquipmentCriticalities.AddAsync(new EquipmentCriticality
        {
            EquipmentId = assessment.EquipmentId,
            CriticalityClass = proposedClass,
            RiskLevel = RiskLevelByClass.GetValueOrDefault(proposedClass, proposedClass),
            SafetyImpact = $"Score {assessment.SafetyScore}/{CriticalityFramework.MaxScore} (Framework {assessment.FrameworkVersion})",
            ProductionImpact = $"Score {assessment.ProductionScore}/{CriticalityFramework.MaxScore} (Framework {assessment.FrameworkVersion})",
            EnvironmentImpact = $"Score {assessment.EnvironmentScore}/{CriticalityFramework.MaxScore} (Framework {assessment.FrameworkVersion})",
            Description = $"Published from approved Criticality Assessment #{assessment.CriticalityAssessmentId} (approved by {approvedBy} on {DateTime.Now:yyyy-MM-dd}).",
            IsActive = true,
            CreatedDate = DateTime.Now,
            CreatedBy = approvedBy
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RejectAsync(int assessmentId, string? reviewedBy)
    {
        var assessment = await _context.CriticalityAssessments.FirstOrDefaultAsync(x => x.CriticalityAssessmentId == assessmentId);
        if (assessment == null) return (false, "ارزیابی یافت نشد.");
        if (assessment.Status != "UNDER_REVIEW") return (false, $"فقط ارزیابی UNDER_REVIEW قابل‌رد است (وضعیت فعلی: {assessment.Status}).");

        assessment.Status = "DRAFT"; // برگشت به DRAFT برای اصلاح - نه حذف
        assessment.ReviewedBy = reviewedBy;

        await _context.SaveChangesAsync();
        return (true, null);
    }
}
