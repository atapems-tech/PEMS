namespace PEMS.Application.Interfaces;

/// <summary>
/// SCRUM-761: the commands of the vendor document register details page that are not already operations of the VDR workflow service (the review steps are): the edit of the details, the hold and its release,
/// the deactivation and the action status of a comment. The module permission of the old controller is the floor of each; the texts are the localized resource texts the controller showed.
/// </summary>
public interface IVdrOperations
{
    [PEMS.Application.Common.PemsOperation("VdrItem", "UpdateDetails", "VDR.Edit")]
    Task<(bool Success, string? Error)> UpdateDetailsAsync(int vdrItemId, string? documentDescription, int? documentClass, DateTime? plannedFirstIssueDate, string? equipmentTag, string? disciplineCodeRaw, string? vendorFormatDocumentNumber, string? userName);

    [PEMS.Application.Common.PemsOperation("VdrItem", "Hold", "VDR.Hold")]
    Task<(bool Success, string? Error)> HoldAsync(int vdrItemId, string? reason, int? actorEmployeeId);

    [PEMS.Application.Common.PemsOperation("VdrItem", "ReleaseHold", "VDR.ReleaseHold")]
    Task<(bool Success, string? Error)> ReleaseHoldAsync(int vdrItemId, string? reason, int? actorEmployeeId);

    [PEMS.Application.Common.PemsOperation("VdrItem", "Deactivate", "VDR.Delete")]
    Task<(bool Success, string? Error)> DeactivateAsync(int vdrItemId, string? reason, string? userName);

    /// <summary>Registers a VDR item by hand (the manual entry form); answers the id of the new item.</summary>
    [PEMS.Application.Common.PemsOperation("VdrItem", "Create", "VDR.Create")]
    Task<int> CreateAsync(int? sequenceNo, int projectId, int vendorId, string? disciplineCodeRaw, int? equipmentId, string documentTypeCode, string documentNumber, string? mrNumber, string? materialCode, string? typologyCode,
        string? documentDescription, string? responsiblePartyCode, int? documentClass, DateTime? plannedFirstIssueDate);

    [PEMS.Application.Common.PemsOperation("VdrItem", "SetCommentAction")]
    Task<(bool Success, string? Error)> SetCommentActionAsync(int commentId, string actionStatus);
}
