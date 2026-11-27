using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public sealed record StrategyCoverageSummaryRow(int Id, int TotalEquipment, int CoveredEquipment, double CoveredPercent, int UnratedEquipment, int UnclassifiedEquipment, int CriticalWithoutStrategyTotal, bool NoCriticalityData);
public sealed record StrategyCoverageGroupRow(string Key, int TotalEquipment, int WithApprovedStrategy, int WithoutStrategy, double CoveragePercent);
public sealed record StrategyUncoveredRow(int EquipmentId, string Tag, string Name, string? EquipmentClassName, string? Area, string? Unit, string? CriticalityClass);
public sealed record StrategyTagIdentityRow(int EquipmentId, string Tag, string Name, string? EquipmentClassName, string? AreaUnit, string? CriticalityClass);
public sealed record StrategyTagAssignmentRow(int AssignmentId, int MaintenanceStrategyId, int StrategyVersion, string OverrideStatus, int PlanCount);
public sealed record StrategyTagPlanRow(int MaintenancePlanId, string PlanNumber, string Title, int? StrategyVersion, DateTime? NextDueDate);
public sealed record StrategyTagWorkOrderRow(int WorkOrderId, string WorkOrderNumber, string Status, string Priority);
public sealed record StrategyListRow(int Id, string? EquipmentClass, string? EquipmentTag, string StrategyType, string FailureMode, string Consequence, string? Frequency, string Status, int Version, int AssignedTags, bool ReviewOverdue, string? Justification);
public sealed record StrategyHeadRow(int Id, string Title, string Status, string StatusCode, int Version, string? EquipmentClass, string StrategyType, string FailureMode, string Consequence, bool HiddenFailure, string? TaskType, string? OwnerDiscipline,
    string? Frequency, string? Source, string? FunctionalFailure, string? FailureEffect, string? Cbm, string? Thresholds, int? PfIntervalDays, int? InspectionIntervalDays, string? Justification, DateTime? ReviewDate, string? Approved, string? LegacyTag,
    bool CanEdit, bool CanSubmit, bool CanReview, bool CanActivate, bool CanRetire, bool CanAssign, bool SeparationNotice);
public sealed record StrategyVersionRow(int Id, int Version, string Status, DateTime? ApprovedAt, bool IsCurrent);
public sealed record StrategyAssignmentRow(int Id, int EquipmentId, string Tag, string EquipmentName, string? EquipmentClass, string Status, string Override, string? OverrideJustification, int PlanCount, DateTime AssignedDate, bool CanRequestOverride, bool CanDecideOverride, bool CanUnassign);
public sealed record StrategyPlanRow(int Id, string PlanNumber, string Tag, string Title, string Version, bool OutOfSync);
public sealed record StrategyHistoryRow(int Id, DateTime When, string? Who, string Action, string? FromStatus, string? ToStatus, string? Summary, string? Comment);
public sealed record StrategyFileAnswer(byte[] Bytes, string FileName, string ContentType);

/// <summary>
/// SCRUM-829: the read screens of MaintenanceStrategyController that are not the list, details or forms: the coverage report (figures, two breakdowns, the uncovered critical equipment and its CSV), the maintenance view of one tag
/// and the failure-mode picker. MaintenanceStrategy.View is the right the controller checked on each of them.
/// </summary>
public interface IStrategyPagesOperations
{
    [PEMS.Application.Common.PemsOperation("StrategyPage", "CoverageSummary", "MaintenanceStrategy.View")]
    Task<List<StrategyCoverageSummaryRow>> CoverageSummaryAsync();

    [PEMS.Application.Common.PemsOperation("StrategyPage", "CoverageByCriticality", "MaintenanceStrategy.View")]
    Task<List<StrategyCoverageGroupRow>> CoverageByCriticalityAsync();

    [PEMS.Application.Common.PemsOperation("StrategyPage", "CoverageByClass", "MaintenanceStrategy.View")]
    Task<List<StrategyCoverageGroupRow>> CoverageByClassAsync();

    [PEMS.Application.Common.PemsOperation("StrategyPage", "UncoveredCritical", "MaintenanceStrategy.View")]
    Task<List<StrategyUncoveredRow>> UncoveredCriticalAsync();

    [PEMS.Application.Common.PemsOperation("StrategyPage", "UncoveredCsv", "MaintenanceStrategy.View")]
    Task<StrategyFileAnswer> UncoveredCsvAsync();

    [PEMS.Application.Common.PemsOperation("StrategyPage", "TagIdentity", "MaintenanceStrategy.View")]
    Task<List<StrategyTagIdentityRow>> TagIdentityAsync(int equipmentId);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "TagAssignments", "MaintenanceStrategy.View")]
    Task<List<StrategyTagAssignmentRow>> TagAssignmentsAsync(int equipmentId);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "TagPlans", "MaintenanceStrategy.View")]
    Task<List<StrategyTagPlanRow>> TagPlansAsync(int equipmentId);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "TagWorkOrders", "MaintenanceStrategy.View")]
    Task<List<StrategyTagWorkOrderRow>> TagWorkOrdersAsync(int equipmentId);

    /// <summary>The failure-mode picker (served as JSON at /x/strategy-failure-modes): the modes of a class with their causes.</summary>
    [PEMS.Application.Common.PemsOperation("StrategyPage", "FailureModes", "MaintenanceStrategy.View")]
    Task<List<FailureModeOptionDto>> FailureModesAsync(int? classId = null, string? q = null);

    /// <summary>The list of strategies (served at /d/strategy-list); the filters of the old page are the columns of the list.</summary>
    [PEMS.Application.Common.PemsOperation("StrategyPage", "List", "MaintenanceStrategy.View")]
    Task<List<StrategyListRow>> ListAsync();

    [PEMS.Application.Common.PemsOperation("StrategyPage", "Head", "MaintenanceStrategy.View")]
    Task<List<StrategyHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "Versions", "MaintenanceStrategy.View")]
    Task<List<StrategyVersionRow>> VersionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "Assignments", "MaintenanceStrategy.View")]
    Task<List<StrategyAssignmentRow>> AssignmentsAsync(int id);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "Plans", "MaintenanceStrategy.View")]
    Task<List<StrategyPlanRow>> PlansAsync(int id);

    [PEMS.Application.Common.PemsOperation("StrategyPage", "History", "MaintenanceStrategy.View")]
    Task<List<StrategyHistoryRow>> HistoryAsync(int id);
}
