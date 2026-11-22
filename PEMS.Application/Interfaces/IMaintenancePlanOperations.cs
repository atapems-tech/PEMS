namespace PEMS.Application.Interfaces;

public sealed record MaintenancePlanRow(int MaintenancePlanId, string PlanNumber, int EquipmentId, string EquipmentTag, string EquipmentName, string EquipmentClass, string CriticalityClass, string Title, string StrategyType, int? StrategyId, int? StrategyVersion,
    string FailureMode, string ScheduleMode, bool HasAssignment, string TriggerType, int? IntervalDays, string MeterType, decimal? MeterInterval, decimal? LastExecutionMeterReading, decimal? NextDueMeterReading, DateTime? LastExecutionDate, DateTime? NextDueDate, bool IsOverdue);

public sealed record MaintenancePlanAssignmentRow(int AssignmentId, string Tag, string StrategyType, string FailureMode, int StrategyVersion, string EquipmentClass);

/// <summary>The values of the plan create form for one strategy assignment: the read-only identity (tag, strategy, failure mode) and the suggested values; the property names are the form field names.</summary>
public sealed record MaintenancePlanFormRow(int Id, string Tag, string Strategy, string FailureMode, string Title, string TriggerType, int? IntervalDays, string SuggestedInterval, string ScheduleMode);

/// <summary>Success, or the first rule error: Error is its resource key, Args fill its {0}. NewId is the plan created.</summary>
public sealed record MaintenancePlanCreateStep(bool Success, string? Error, object?[]? Args = null, int? NewId = null);

/// <summary>
/// The maintenance plans (SCRUM-724) as operations: the list and the four commands of MaintenancePlanController other than the plan form (record a meter reading, generate one work order, generate every due work order, deactivate).
/// A generated work order is brought under the work order lifecycle governance like before. The create form (SCRUM-817) is a choice page, a form with the assignment as its record and one create operation.
/// </summary>
public interface IMaintenancePlanOperations
{
    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "List", "MaintenancePlan.View")]
    Task<List<MaintenancePlanRow>> PlansAsync();

    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "RecordMeterReading", "MaintenancePlan.Edit")]
    Task<(bool Success, string? Error)> RecordMeterReadingAsync(int planId, decimal currentReading);

    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "GenerateWorkOrder", "MaintenancePlan.Edit")]
    Task<(bool Success, string? Error)> GenerateWorkOrderAsync(List<string> userRoles, int? employeeId, int planId);

    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "GenerateDueWorkOrders", "MaintenancePlan.Edit")]
    Task<(bool Success, string? Error)> GenerateDueWorkOrdersAsync(List<string> userRoles, int? employeeId);

    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "Deactivate", "MaintenancePlan.Edit")]
    Task<(bool Success, string? Error)> DeactivateAsync(int planId);

    /// <summary>SCRUM-817: the assignments a plan can be created from (the choice step of the create page); optionally limited to one tag or strategy.</summary>
    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "AssignmentChoices", "MaintenancePlan.Create")]
    Task<List<MaintenancePlanAssignmentRow>> AssignmentChoicesAsync(int? equipmentId = null, int? strategyId = null);

    /// <summary>SCRUM-817: the form of one assignment (the tag is read-only and comes from the assignment, never from the client).</summary>
    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "FormOf", "MaintenancePlan.Create")]
    Task<List<MaintenancePlanFormRow>> FormOfAsync(int id);

    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "JobPlanChoices", "MaintenancePlan.Create")]
    Task<List<ReportChoiceRow>> JobPlanChoicesAsync();

    /// <summary>SCRUM-817: creates the plan from an assignment; the equipment is derived on the server from the assignment, never accepted from the client.</summary>
    [PEMS.Application.Common.PemsOperation("MaintenancePlanPage", "Create", "MaintenancePlan.Create")]
    Task<MaintenancePlanCreateStep> CreateAsync(PEMS.Application.DTOs.PlanInputDto input, List<string> userRoles, int? employeeId, string? userName);
}
