using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 4» بند ۱۷-۲۰/۳۱: هر RCMDecision یک توصیه‌ی سطح کلاس است - هرگز مستقیم یک
/// MaintenanceStrategy واقعی نمی‌سازد (بند ۲۰: تایید انسانی روی خودِ توصیه لازم است، و ساخت
/// MaintenanceStrategy واقعی روی یک Equipment مشخص همچنان یک اقدام کاملاً جدا و دستی مهندس
/// می‌ماند - این سرویس فقط توصیه را ثبت/تایید می‌کند).
/// </summary>
public class RCMService : IRCMService
{
    private readonly PEMSDbContext _context;

    public RCMService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<RCMAnalysisDto>> GetForEquipmentClassAsync(int equipmentClassId)
    {
        var analyses = await _context.RCMAnalyses
            .AsNoTracking()
            .Where(x => x.EquipmentClassId == equipmentClassId && x.IsActive)
            .Include(x => x.EquipmentClass)
            .OrderByDescending(x => x.Revision)
            .ToListAsync();

        var result = new List<RCMAnalysisDto>();
        foreach (var a in analyses)
            result.Add(await MapAsync(a));

        return result;
    }

    public async Task<RCMAnalysisDto?> GetByIdAsync(int rcmAnalysisId)
    {
        var analysis = await _context.RCMAnalyses
            .AsNoTracking()
            .Include(x => x.EquipmentClass)
            .FirstOrDefaultAsync(x => x.RCMAnalysisId == rcmAnalysisId);

        return analysis == null ? null : await MapAsync(analysis);
    }

    private async Task<RCMAnalysisDto> MapAsync(RCMAnalysis a)
    {
        var decisions = await _context.RCMDecisions
            .AsNoTracking()
            .Where(x => x.RCMAnalysisId == a.RCMAnalysisId)
            .Include(x => x.FMEAItem!).ThenInclude(x => x.FailureMode)
            .Include(x => x.MaintenanceStrategyType)
            .Include(x => x.RecommendedPMTemplate)
            .ToListAsync();

        return new RCMAnalysisDto
        {
            RCMAnalysisId = a.RCMAnalysisId,
            EquipmentClassId = a.EquipmentClassId,
            EquipmentClassName = a.EquipmentClass?.Name ?? string.Empty,
            FMEAAnalysisId = a.FMEAAnalysisId,
            Revision = a.Revision,
            Status = a.Status,
            PreparedBy = a.PreparedBy,
            ReviewedBy = a.ReviewedBy,
            ApprovedBy = a.ApprovedBy,
            ApprovedAt = a.ApprovedAt,
            Decisions = decisions.Select(x => new RCMDecisionDto
            {
                RCMDecisionId = x.RCMDecisionId,
                RCMAnalysisId = x.RCMAnalysisId,
                FMEAItemId = x.FMEAItemId,
                FailureModeName = x.FMEAItem?.FailureMode?.Name ?? string.Empty,
                DecisionType = x.DecisionType,
                MaintenanceStrategyTypeId = x.MaintenanceStrategyTypeId,
                MaintenanceStrategyTypeName = x.MaintenanceStrategyType?.Name,
                RecommendedPMTemplateId = x.RecommendedPMTemplateId,
                RecommendedPMTemplateName = x.RecommendedPMTemplate?.Name,
                Rationale = x.Rationale,
                ConsequenceCategory = x.ConsequenceCategory,
                Status = x.Status,
                ApprovedBy = x.ApprovedBy,
                ApprovedAt = x.ApprovedAt
            }).ToList()
        };
    }

    public async Task<int> CreateDraftAsync(int equipmentClassId, int fmeaAnalysisId, string preparedBy)
    {
        var lastRevision = await _context.RCMAnalyses
            .Where(x => x.EquipmentClassId == equipmentClassId)
            .Select(x => (int?)x.Revision)
            .MaxAsync() ?? 0;

        var analysis = new RCMAnalysis
        {
            EquipmentClassId = equipmentClassId,
            FMEAAnalysisId = fmeaAnalysisId,
            Revision = lastRevision + 1,
            Status = "DRAFT",
            PreparedBy = preparedBy,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        _context.RCMAnalyses.Add(analysis);
        await _context.SaveChangesAsync();

        return analysis.RCMAnalysisId;
    }

    public async Task<int> AddDecisionAsync(int rcmAnalysisId, int fmeaItemId, string decisionType,
        int? maintenanceStrategyTypeId, int? recommendedPMTemplateId, string? rationale,
        string consequenceCategory)
    {
        var analysis = await _context.RCMAnalyses.FirstOrDefaultAsync(x => x.RCMAnalysisId == rcmAnalysisId);
        if (analysis == null) throw new InvalidOperationException("RCM analysis not found.");
        if (analysis.Status != "DRAFT")
            throw new InvalidOperationException($"Cannot add decisions to an RCM analysis with status '{analysis.Status}'. Only DRAFT analyses are editable.");

        var decision = new RCMDecision
        {
            RCMAnalysisId = rcmAnalysisId,
            FMEAItemId = fmeaItemId,
            DecisionType = decisionType,
            MaintenanceStrategyTypeId = maintenanceStrategyTypeId,
            RecommendedPMTemplateId = recommendedPMTemplateId,
            Rationale = rationale,
            ConsequenceCategory = consequenceCategory,
            Status = "DRAFT",
            CreatedDate = DateTime.Now
        };

        _context.RCMDecisions.Add(decision);
        await _context.SaveChangesAsync();

        return decision.RCMDecisionId;
    }

    public async Task<bool> ApproveDecisionAsync(int rcmDecisionId, string approvedBy)
    {
        var decision = await _context.RCMDecisions.FirstOrDefaultAsync(x => x.RCMDecisionId == rcmDecisionId);
        if (decision == null || decision.Status != "DRAFT") return false;

        decision.Status = "APPROVED";
        decision.ApprovedBy = approvedBy;
        decision.ApprovedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SubmitForReviewAsync(int rcmAnalysisId, string reviewedBy)
    {
        var analysis = await _context.RCMAnalyses.FirstOrDefaultAsync(x => x.RCMAnalysisId == rcmAnalysisId);
        if (analysis == null || analysis.Status != "DRAFT") return false;

        analysis.Status = "UNDER_REVIEW";
        analysis.ReviewedBy = reviewedBy;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveAsync(int rcmAnalysisId, string approvedBy)
    {
        var analysis = await _context.RCMAnalyses.FirstOrDefaultAsync(x => x.RCMAnalysisId == rcmAnalysisId);
        if (analysis == null || analysis.Status != "UNDER_REVIEW") return false;

        var previousApproved = await _context.RCMAnalyses
            .Where(x => x.EquipmentClassId == analysis.EquipmentClassId && x.Status == "APPROVED")
            .ToListAsync();
        foreach (var prev in previousApproved)
            prev.Status = "SUPERSEDED";

        analysis.Status = "APPROVED";
        analysis.ApprovedBy = approvedBy;
        analysis.ApprovedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(int rcmAnalysisId, string reviewedBy)
    {
        var analysis = await _context.RCMAnalyses.FirstOrDefaultAsync(x => x.RCMAnalysisId == rcmAnalysisId);
        if (analysis == null || analysis.Status != "UNDER_REVIEW") return false;

        analysis.Status = "DRAFT";
        analysis.ReviewedBy = reviewedBy;
        await _context.SaveChangesAsync();
        return true;
    }
}
