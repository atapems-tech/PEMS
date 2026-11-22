using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-691, 693: the read pages of the equipment classification and the equipment criticality list, the logic of the controllers moved without change (see <see cref="IRegisterPagesOperations"/>).</summary>
public sealed class RegisterPagesOperations(PEMSDbContext context, IEquipmentClassificationService classification, IEquipmentClassCoverageService coverage) : IRegisterPagesOperations
{
    private const int RowLimit = 5000;

    public async Task<List<ClassificationSummaryRow>> ClassificationSummaryAsync()
    {
        var d = await classification.GetDashboardAsync();
        return [new(d.TotalActiveEquipment, d.ClassifiedCount, d.UnclassifiedCount, d.CoveragePercent, d.PendingReviewCount)];
    }

    public async Task<List<ClassificationBreakdownRow>> ClassificationBreakdownAsync()
    {
        var d = await classification.GetDashboardAsync();
        var rows = new List<ClassificationBreakdownRow>();
        void Add(string group, IEnumerable<CountByKeyDto> items) => rows.AddRange(items.Select(i => new ClassificationBreakdownRow(group, i.Key, i.Count)));
        Add("By confidence", d.ByConfidence);
        Add("By source", d.BySource);
        Add("By equipment class", d.ByEquipmentClass);
        Add("Pending suggestions by confidence", d.PendingSuggestionsByConfidence);
        Add("Unclassified by discipline", d.UnclassifiedByDiscipline);
        Add("Top unclassified equipment types", d.TopUnclassifiedEquipmentTypes);
        Add("Top unclassified equipment names", d.TopUnclassifiedEquipmentNames);
        Add("Top unclassified tag patterns", d.TopUnclassifiedTagPatterns);
        return rows;
    }

    public async Task<List<ClassificationSuggestionRow>> ReviewQueueAsync(string? status = null)
        => (await classification.GetReviewQueueAsync(1, RowLimit, string.IsNullOrWhiteSpace(status) ? "PENDING" : status)).Items
            .Select(i => new ClassificationSuggestionRow(i.SuggestionId, i.TagNumber, i.EquipmentName, i.EquipmentType, i.DisciplineName ?? "", i.SuggestedEquipmentClassName, i.Confidence, i.Source, i.Reason, i.Status,
                i.ReviewedBy ?? "", i.ReviewedAt, i.CreatedDate)).ToList();

    public async Task<List<CoverageSummaryRow>> CoverageSummaryAsync()
    {
        var m = await coverage.GetCoverageMatrixAsync();
        return [new(m.ClassesWithEquipment, m.ClassesWithDirectOrInheritedActivities, m.ClassesWithDirectOrInheritedPMTemplates, m.ClassesWithDirectOrInheritedFailureModes, m.ClassesWithDirectOrInheritedStandards,
            m.ClassesWithJobPlans, m.ActivityCoveragePercent, m.PMTemplateCoveragePercent, m.FailureModeCoveragePercent, m.StandardsCoveragePercent)];
    }

    public async Task<List<ClassCoverageRow>> CoverageClassesAsync()
        => (await coverage.GetCoverageMatrixAsync()).Classes
            .Select(c => new ClassCoverageRow(c.EquipmentClassId, c.Code, c.Name, c.ParentClassName ?? "", c.EquipmentCount, c.TotalActivityCoverage, c.DirectPMTemplateCount + c.InheritedPMTemplateCount,
                c.DirectJobPlanCount + c.InheritedJobPlanCount, c.TotalFailureModeCoverage, c.DirectStandardReferenceCount + c.InheritedStandardReferenceCount, c.StrategyTypeCount, string.Join(" | ", c.GapReasons))).ToList();

    public async Task<List<CriticalityRow>> CriticalityListAsync(string? criticalityClass = null)
    {
        var query = context.EquipmentCriticalities.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(criticalityClass)) query = query.Where(x => x.CriticalityClass == criticalityClass);
        return (await query.OrderBy(x => x.CriticalityClass).ThenBy(x => x.Equipment!.TagNumber).ThenBy(x => x.EquipmentCriticalityId).Take(RowLimit)
            .Select(x => new { x.EquipmentCriticalityId, Tag = x.Equipment!.TagNumber, x.CriticalityClass, x.RiskLevel, x.SafetyImpact, x.ProductionImpact, x.EnvironmentImpact }).ToListAsync())
            .Select(x => new CriticalityRow(x.EquipmentCriticalityId, x.Tag ?? "", x.CriticalityClass, x.RiskLevel ?? "", x.SafetyImpact ?? "", x.ProductionImpact ?? "", x.EnvironmentImpact ?? "")).ToList();
    }

    public async Task<List<CriticalityCountsRow>> CriticalityCountsAsync()
    {
        var counts = await context.EquipmentCriticalities.AsNoTracking().Where(x => x.IsActive).GroupBy(x => x.CriticalityClass).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        // equipment with no analysis yet: EXISTS in SQL, never a full load
        var unassessed = await context.Equipments.CountAsync(x => x.IsActive && !context.EquipmentCriticalities.Any(c => c.EquipmentId == x.EquipmentId && c.IsActive));
        return [new(1, counts.GetValueOrDefault("A"), counts.GetValueOrDefault("B"), counts.GetValueOrDefault("C"), unassessed)];
    }
}
