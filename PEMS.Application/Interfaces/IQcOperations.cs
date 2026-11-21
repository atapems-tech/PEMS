namespace PEMS.Application.Interfaces;

/// <summary>
/// The write commands of QCController as operations (SCRUM-694): the ITP line items, approval, hold point notice and inspection, and the NCR disposition, approval and closing. QCController checked no module
/// permission (any signed-in user), so the operations carry none either: the host's own checks (signed in, antiforgery) are the gate, exactly as before. The creates that redirect to the new record stay in the controller.
/// </summary>
public interface IQcOperations
{
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
