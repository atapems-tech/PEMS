namespace PEMS.Application.Interfaces;

/// <summary>The one row of the work request page: the head, the workflow in one line and what the user can do next (the flags drive the row actions).</summary>
public sealed record WorkRequestHeadRow(int Id, int WorkRequestId, string RequestNumber, string Title, string Status, string Priority, string RequestedBy, DateTime RequestedDate, string Equipment, string Asset, string Location,
    string CriticalityClass, DateTime? ResponseDueDate, DateTime? ResolutionDueDate, string FailureMode, string FailureEffect, decimal? DowntimeHours, string ReportedCondition, string SuggestedDiscipline, string Description,
    bool IsEmergency, bool IsHseRelated, string Risk, string PriorityRationale, string RejectionReason, string ConvertedTo, int? LinkedWorkOrderId, string Workflow, string WorkflowStatus,
    bool CanSubmit, bool CanConvert, bool NeedsEquipment, bool IsLegacy, string NextStatus, bool CanReject);

public sealed record WorkRequestStageRow(int Id, int Order, string Stage, string State, string Waiting);
public sealed record WorkRequestWorkflowActionRow(string ActionType, int WorkRequestId, string Label, string ToStage, bool Allowed, string DeniedReason);
public sealed record WorkRequestWorkflowHistoryRow(int Id, DateTime ActionDate, string Action, string FromStage, string ToStage, string By, string Reason);
public sealed record WorkRequestStatusHistoryRow(int Id, string FromStatus, string ToStatus, string ChangedBy, DateTime ChangedDate, string Reason);

/// <summary>
/// SCRUM-817: the work request details page (the former WorkRequestController.Details) as read operations: the head with what the user may do next, the workflow stages, the workflow actions available to the user
/// (the workflow decides, the command checks again), the workflow history and the business audit. The commands were already actions. The work request id is the parameter of every read.
/// </summary>
public interface IWorkRequestDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("WorkRequestDetails", "Head", "WorkRequest.View")]
    Task<List<WorkRequestHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkRequestDetails", "Stages", "WorkRequest.View")]
    Task<List<WorkRequestStageRow>> StagesAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkRequestDetails", "WorkflowActions", "WorkRequest.View")]
    Task<List<WorkRequestWorkflowActionRow>> WorkflowActionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkRequestDetails", "WorkflowHistory", "WorkRequest.View")]
    Task<List<WorkRequestWorkflowHistoryRow>> WorkflowHistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("WorkRequestDetails", "StatusHistory", "WorkRequest.View")]
    Task<List<WorkRequestStatusHistoryRow>> StatusHistoryAsync(int id);
}
