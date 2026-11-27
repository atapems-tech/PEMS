namespace PEMS.Application.Interfaces;

/// <summary>
/// The one-row head of the work order details page: the work order itself, the totals, the failure analysis and every flag the page needs to show or hide a control (legacy path buttons, the discipline/executor/schedule forms, the
/// sections of the section matrix). The flags are the rules of the old controller and view: the permission, the status and the phase matrix of IWorkOrderSectionGuard.
/// </summary>
public sealed record WorkOrderHeadRow(int Id, string WorkOrderNumber, string Status, string StatusCode, string Priority, string WorkOrderType, int EquipmentId, string? EquipmentTag, string? EquipmentName, string? Assigned, string? PlannedWindow,
    DateTime? StartDate, DateTime? CompletionDate, string? FromPlan, string? Description, string? OriginatingDiscipline, string? ResponsibleDiscipline, int? SourceWorkRequestId, string? FailureAnalysis, decimal LaborCost, decimal SpareCost,
    bool IsLegacy, bool HasPendingReview, string? NextStatus, string? Warnings, bool CanAdvanceLegacy, bool CanAdvanceToCompleted, bool CanHold, bool CanResume, bool CanReject, bool CanCancel, bool HasWorkflowActions, bool IsInProgress, bool IsCorrectiveType,
    bool CanReopen, bool CtlResponsibleDiscipline, bool CtlExecutor, bool CtlSchedule, bool CtlSkills, bool CtlMaterials, bool CtlCrew, bool CtlLabor, bool CtlSpares, bool CtlReview, bool CtlTasks,
    string? AdvanceForm, int? ResponsibleDisciplineId, string? PlannedStartInput, string? PlannedFinishInput, decimal TotalCost);
public sealed record WorkOrderStageRow(int Id, int Order, string Stage, string State, string WaitingFor, string Step, string StepLabel);
/// <summary>One action of the workflow; <c>FormKey</c> is the form that runs it: with or without the comment being required, and with the completion data (and the failure analysis for corrective and emergency work) while the work is in progress.</summary>
public sealed record WorkOrderActionRow(string Id, string Label, string ToStage, bool Allowed, string? DeniedReason, bool NeedsReason, string FormKey);
public sealed record WorkOrderWorkflowHistoryRow(int Id, DateTime When, string Action, string From, string To, string By, string? Comment);
public sealed record WorkOrderStatusHistoryRow(int Id, DateTime When, string FromStatus, string ToStatus, string? Reason, string By);

public sealed record WorkOrderLaborRow(int Id, DateTime WorkDate, string Employee, string? Team, decimal RegularHours, decimal OvertimeHours, decimal Cost, string? Notes);
public sealed record WorkOrderSpareRow(int Id, string Material, decimal Quantity, decimal? UnitCost, decimal? TotalCost, decimal ReturnedQuantity, DateTime UsageDate, bool CanReturn);
public sealed record WorkOrderTaskRow(int Id, int Number, string Title, string? Description, bool Done, string? CompletedBy, DateTime? CompletedDate, bool CanComplete, bool CanReopen);
public sealed record WorkOrderSkillRow(int Id, string Skill, string? MinimumLevel, decimal? PlannedHours, bool CanRemove);
public sealed record WorkOrderMaterialRow(int Id, string Material, decimal RequiredQuantity, string SupplyCode, string Sourced, string? Requisitions, bool CanSetSupply, bool CanRemove);
public sealed record WorkOrderIntakeRow(int Id, string State, bool CanRequest, bool IsOpen);
public sealed record WorkOrderCrewRow(int Id, string Code, string Name, string Members);
public sealed record WorkOrderReviewRow(int Id, string Discipline, string Status, bool CanRespond);
public sealed record WorkOrderFindingRow(int Id, string Discipline, int Cycle, string Decision, string? Findings, string? FailureMode, string? FailureCause, DateTime RespondedDate);
public sealed record WorkOrderChoiceRow(string Value, string Label);

/// <summary>SCRUM-831: the read side of WorkOrderController.Details, in sections (the page is a definition over these rows; the commands are the existing work-order-* actions). WorkOrder.View is the right the controller checked.</summary>
public interface IWorkOrderDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Head", "WorkOrder.View")]
    Task<List<WorkOrderHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Stages", "WorkOrder.View")]
    Task<List<WorkOrderStageRow>> StagesAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "WorkflowActions", "WorkOrder.View")]
    Task<List<WorkOrderActionRow>> WorkflowActionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "WorkflowHistory", "WorkOrder.View")]
    Task<List<WorkOrderWorkflowHistoryRow>> WorkflowHistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "StatusHistory", "WorkOrder.View")]
    Task<List<WorkOrderStatusHistoryRow>> StatusHistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Labor", "WorkOrder.View")]
    Task<List<WorkOrderLaborRow>> LaborAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Spares", "WorkOrder.View")]
    Task<List<WorkOrderSpareRow>> SparesAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Tasks", "WorkOrder.View")]
    Task<List<WorkOrderTaskRow>> TasksAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Skills", "WorkOrder.View")]
    Task<List<WorkOrderSkillRow>> SkillsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Materials", "WorkOrder.View")]
    Task<List<WorkOrderMaterialRow>> MaterialsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Intake", "WorkOrder.View")]
    Task<List<WorkOrderIntakeRow>> IntakeAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Crews", "WorkOrder.View")]
    Task<List<WorkOrderCrewRow>> CrewsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Review", "WorkOrder.View")]
    Task<List<WorkOrderReviewRow>> ReviewAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "Findings", "WorkOrder.View")]
    Task<List<WorkOrderFindingRow>> FindingsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "TeamOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> TeamOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "EmployeeOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> EmployeeOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "DisciplineOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> DisciplineOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "MaterialItemOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> MaterialItemOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "SkillOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> SkillOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "FailureModeOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> FailureModeOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "FailureCauseOptions", "WorkOrder.View")]
    Task<List<WorkOrderChoiceRow>> FailureCauseOptionsAsync(int id);

    /// <summary>The linear step of a legacy work order (no workflow instance): the next status of the flow, with the rules of the old controller (Edit; closing needs Approve; completing waits for an open discipline review).</summary>
    [PEMS.Application.Common.PemsOperation("WorkOrderDetail", "LegacyAdvance", "WorkOrder.Edit")]
    Task<(bool Success, string? Error)> LegacyAdvanceAsync(int id, int? assignedTeamId = null, string? failureModeCode = null, string? failureCauseCode = null, string? rootCause = null, decimal? downtimeHours = null, string? reason = null,
        string? failureEffect = null, string? correctiveAction = null, decimal? contractorCost = null, string? contractorName = null);
}
