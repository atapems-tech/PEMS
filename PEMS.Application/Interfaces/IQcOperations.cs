namespace PEMS.Application.Interfaces;

public sealed record QcSummaryRow(int OpenNcrCount, int ActiveItpCount);
public sealed record QcItpRow(int Id, string ItpNumber, string Title, string Discipline, string Equipment, string Status, int Revision, DateTime CreatedDate);
public sealed record QcItpHeadRow(int Id, string ItpNumber, string Title, string Status, int Revision, string Discipline, string Equipment, string Package, string Contract, string ReferenceCodeStandard, string Description, DateTime? ApprovedDate);
public sealed record QcLineItemRow(int Id, int SequenceNo, string Activity, string Discipline, string InspectionCategory, string AcceptanceCriteria, string ReferenceCodeStandard, string ResponsibleParty, string Status, DateTime? NotifiedDate);
public sealed record QcNcrRow(int Id, string NcrNumber, string Discipline, string Equipment, string Severity, string Status, string Disposition, DateTime RaisedDate, DateTime? ClosedDate);
public sealed record QcNcrHeadRow(int Id, string NcrNumber, string Discipline, string Equipment, string Severity, string Status, string Description, string Disposition, string RootCause, string CorrectiveAction, string PreventiveAction, DateTime RaisedDate, DateTime? DispositionedDate, DateTime? ClosedDate, string LineItem);
public sealed record QcKpiRow(int Id, int TotalNcr, double ClosedOnTimeRate);
public sealed record QcKpiGroupRow(string Group, int Total, int Open, int Critical, double AvgDaysToClose);
public sealed record QcOption(string Value, string Label);

/// <summary>
/// QC / QA as operations (SCRUM-694, 742): the hub counts and recent records, the ITP and NCR lists and heads, the ITP line items, the KPI report, the choices of the two create forms (reads); the ITP and NCR creates; the ITP line items, approval,
/// hold point notice and inspection; and the NCR disposition, approval and closing. QCController checked no module permission (any signed-in user), so the operations carry none either: the host's own checks (signed in, antiforgery) are the gate,
/// exactly as before.
/// </summary>
public interface IQcOperations
{
    [PEMS.Application.Common.PemsOperation("Qc", "Summary")]
    Task<List<QcSummaryRow>> SummaryAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "RecentNcrs")]
    Task<List<QcNcrRow>> RecentNcrsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "RecentItps")]
    Task<List<QcItpRow>> RecentItpsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "ItpList")]
    Task<List<QcItpRow>> ItpListAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "ItpHead")]
    Task<List<QcItpHeadRow>> ItpHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("Qc", "ItpLineItems")]
    Task<List<QcLineItemRow>> ItpLineItemsAsync(int id);

    [PEMS.Application.Common.PemsOperation("Qc", "NcrList")]
    Task<List<QcNcrRow>> NcrListAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "NcrHead")]
    Task<List<QcNcrHeadRow>> NcrHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("Qc", "Kpi")]
    Task<List<QcKpiRow>> KpiAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "KpiByDiscipline")]
    Task<List<QcKpiGroupRow>> KpiByDisciplineAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "KpiBySeverity")]
    Task<List<QcKpiGroupRow>> KpiBySeverityAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "KpiByMonth")]
    Task<List<QcKpiGroupRow>> KpiByMonthAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "ProjectOptions")]
    Task<List<QcOption>> ProjectOptionsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "DisciplineOptions")]
    Task<List<QcOption>> DisciplineOptionsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "EquipmentOptions")]
    Task<List<QcOption>> EquipmentOptionsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "PackageOptions")]
    Task<List<QcOption>> PackageOptionsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "ContractOptions")]
    Task<List<QcOption>> ContractOptionsAsync();

    [PEMS.Application.Common.PemsOperation("Qc", "ItpCreate")]
    Task<(bool Success, string? Error)> ItpCreateAsync(string itpNumber, int projectId, int qcDisciplineId, string title, int? equipmentId = null, string? referenceCodeStandard = null, string? description = null,
        int? procurementPackageId = null, int? contractId = null);

    [PEMS.Application.Common.PemsOperation("Qc", "NcrCreate")]
    Task<(bool Success, string? Error)> NcrCreateAsync(string ncrNumber, int projectId, int qcDisciplineId, string description, string severity, int? equipmentId = null, int? itpLineItemId = null);

    [PEMS.Application.Common.PemsOperation("Qc", "ItpAddLineItem")]
    Task<(bool Success, string? Error)> ItpAddLineItemAsync(int itpId, string activity, int qcDisciplineId, string inspectionCategory, string? acceptanceCriteria = null, string? referenceCodeStandard = null, string? responsibleParty = null);

    [PEMS.Application.Common.PemsOperation("Qc", "ItpApprove")]
    Task<(bool Success, string? Error)> ItpApproveAsync(int id);

    [PEMS.Application.Common.PemsOperation("Qc", "ItpNotifyHoldPoint")]
    Task<(bool Success, string? Error)> ItpNotifyHoldPointAsync(int lineItemId);

    [PEMS.Application.Common.PemsOperation("Qc", "ItpPerformInspection")]
    Task<(bool Success, string? Error)> ItpPerformInspectionAsync(int lineItemId, string result, string? inspector = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("Qc", "NcrDisposition")]
    Task<(bool Success, string? Error)> NcrDispositionAsync(int id, string disposition, string? rootCause = null, string? correctiveAction = null, string? preventiveAction = null);

    [PEMS.Application.Common.PemsOperation("Qc", "NcrApprove")]
    Task<(bool Success, string? Error)> NcrApproveAsync(int id, string decision, string? comment = null);

    [PEMS.Application.Common.PemsOperation("Qc", "NcrClose")]
    Task<(bool Success, string? Error)> NcrCloseAsync(int id);
}
