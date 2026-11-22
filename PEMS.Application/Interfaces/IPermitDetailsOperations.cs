namespace PEMS.Application.Interfaces;

/// <summary>The one row of the permit page: the head, what the user can do next (the flags drive the row actions) and the notices of the authority checks that would refuse an action.</summary>
public sealed record PermitHeadRow(int Id, string PermitNumber, string Title, string PermitType, string Status, string Priority, string Description, string Equipment, string Location, string WorkOrder,
    string RequestedBy, string Issuer, string Receiver, DateTime? ValidFrom, DateTime? ValidTo, DateTime? ClosedDate, string CancellationReason, string RevocationReason,
    string NextStatus, bool CanSubmitForApproval, bool CanRespond, bool GateEligible, bool CanEmergencyOverride, string Notices);

public sealed record PermitApprovalRow(int Id, string Stage, string Status, string Owner, DateTime? Started, DateTime? Completed);
public sealed record PermitApprovalHistoryRow(DateTime ActionDate, string ActionType, string FromStage, string ToStage, string Decision, string Role, string Comment);
public sealed record PermitIsolationRow(int Id, int PermitId, string EnergyType, string Location, string RequiredState, string Method, string LockNumber, string TagNumber, string Status, string Equipment,
    string IsolatedBy, DateTime? IsolatedDate, string VerifiedBy, DateTime? VerifiedDate, bool PermitOpen);
public sealed record PermitGasTestRow(int Id, int PermitId, string Location, DateTime TestedAt, string Tester, string Instrument, string CalibrationStatus, string Status, DateTime? ValidUntil, bool IsExpired,
    string Readings, string InvalidationReason, bool CanInvalidate);
public sealed record PermitSimopsConflictRow(int Id, int PermitId, string OtherPermit, int OtherPermitId, string Basis, string Severity, string Status, string RaisedBy, string Notes);
public sealed record PermitSimopsCandidateRow(int Id, int PermitId, string PermitNumber, string Title, string PermitType, string Status, string Equipment, string MatchBasis, string TimeWindow, bool HasConflict);
public sealed record PermitCandidateWorkOrderRow(int Id, string WorkOrderNumber, string Equipment, DateTime? StartDate, DateTime? CompletionDate);
public sealed record PermitHandoverRow(int Id, int HandoverId, string Status, DateTime? PreparedDate);
public sealed record PermitGateCheckRow(int Id, string Category, string Name, bool Passed, string Detail, bool Advisory);
public sealed record PermitGateDecisionRow(int Id, string FromStatus, string ToStatus, string Result, string Actor, DateTime DecisionDate, bool IsEmergencyOverride, string Reason);
public sealed record PermitStatusHistoryRow(int Id, string FromStatus, string ToStatus, string ChangedBy, DateTime ChangedDate, string Reason);

/// <summary>
/// SCRUM-817: the permit details page (the former PermitController.Details) as read operations: the head with what the user may do next, the approval chain and its history, the isolation points, the gas tests,
/// SIMOPS conflicts and candidates, the shift handover backlinks, the execution gate and its decisions, and the status history. The commands were already actions; nothing here changes data. The
/// permit id is the parameter of every read (the filter of the child grid of the page).
/// </summary>
public interface IPermitDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("PermitDetails", "Head", "Permit.View")]
    Task<List<PermitHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "Approval", "Permit.View")]
    Task<List<PermitApprovalRow>> ApprovalAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "ApprovalHistory", "Permit.View")]
    Task<List<PermitApprovalHistoryRow>> ApprovalHistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "Isolation", "Permit.View")]
    Task<List<PermitIsolationRow>> IsolationAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "GasTests", "Permit.View")]
    Task<List<PermitGasTestRow>> GasTestsAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "SimopsConflicts", "Permit.View")]
    Task<List<PermitSimopsConflictRow>> SimopsConflictsAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "SimopsCandidates", "Permit.View")]
    Task<List<PermitSimopsCandidateRow>> SimopsCandidatesAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "CandidateWorkOrders", "Permit.View")]
    Task<List<PermitCandidateWorkOrderRow>> CandidateWorkOrdersAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "Handovers", "Permit.View")]
    Task<List<PermitHandoverRow>> HandoversAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "GateChecks", "Permit.View")]
    Task<List<PermitGateCheckRow>> GateChecksAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "GateDecisions", "Permit.View")]
    Task<List<PermitGateDecisionRow>> GateDecisionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("PermitDetails", "StatusHistory", "Permit.View")]
    Task<List<PermitStatusHistoryRow>> StatusHistoryAsync(int id);
}
