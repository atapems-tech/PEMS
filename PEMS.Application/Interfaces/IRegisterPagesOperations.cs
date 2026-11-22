namespace PEMS.Application.Interfaces;

public sealed record ClassificationSummaryRow(int TotalActiveEquipment, int ClassifiedCount, int UnclassifiedCount, double CoveragePercent, int PendingReviewCount);
public sealed record ClassificationBreakdownRow(string Group, string Key, int Count);
public sealed record ClassificationSuggestionRow(int Id, string TagNumber, string EquipmentName, string EquipmentType, string Discipline, string SuggestedClass, string Confidence, string Source, string Reason, string Status,
    string ReviewedBy, DateTime? ReviewedAt, DateTime CreatedDate);
public sealed record ClassCoverageRow(int Id, string Code, string Name, string ParentClass, int EquipmentCount, int Activities, int PmTemplates, int JobPlans, int FailureModes, int StandardReferences, int StrategyTypes, string Gaps);
public sealed record CoverageSummaryRow(int ClassesWithEquipment, int ClassesWithActivities, int ClassesWithPmTemplates, int ClassesWithFailureModes, int ClassesWithStandards, int ClassesWithJobPlans,
    double ActivityCoveragePercent, double PmTemplateCoveragePercent, double FailureModeCoveragePercent, double StandardsCoveragePercent);
public sealed record CriticalityRow(int Id, string EquipmentTag, string CriticalityClass, string RiskLevel, string SafetyImpact, string ProductionImpact, string EnvironmentImpact);
public sealed record CriticalityCountsRow(int Id, int ClassA, int ClassB, int ClassC, int Unassessed);

/// <summary>
/// The read pages of the equipment classification (dashboard, review queue, coverage) and the equipment criticality list as operations (SCRUM-691, 693): the permissions are the ones the controllers checked
/// (MaintenanceMasterData.View and EquipmentCriticality.View). The accept and reject of a suggestion and the criticality assessment are commands of the register operations.
/// </summary>
public interface IRegisterPagesOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "Summary", "MaintenanceMasterData.View")]
    Task<List<ClassificationSummaryRow>> ClassificationSummaryAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "Breakdown", "MaintenanceMasterData.View")]
    Task<List<ClassificationBreakdownRow>> ClassificationBreakdownAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "ReviewQueue", "MaintenanceMasterData.View")]
    Task<List<ClassificationSuggestionRow>> ReviewQueueAsync(string? status = null);

    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "CoverageSummary", "MaintenanceMasterData.View")]
    Task<List<CoverageSummaryRow>> CoverageSummaryAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "CoverageClasses", "MaintenanceMasterData.View")]
    Task<List<ClassCoverageRow>> CoverageClassesAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentCriticality", "List", "EquipmentCriticality.View")]
    Task<List<CriticalityRow>> CriticalityListAsync(string? criticalityClass = null);

    [PEMS.Application.Common.PemsOperation("EquipmentCriticality", "Counts", "EquipmentCriticality.View")]
    Task<List<CriticalityCountsRow>> CriticalityCountsAsync();
}
