using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-761: the details edit, hold, release, deactivation and comment action status of VDRController (same services, same texts).</summary>
public sealed class VdrOperations(IVDRService vdr, IDocumentHoldService hold, IDocumentCommentService comments, IStringLocalizer<SharedResource> localizer) : IVdrOperations
{
    private string T(string key) { var l = localizer[key]; return l.ResourceNotFound ? key : l.Value; }

    public async Task<(bool Success, string? Error)> UpdateDetailsAsync(int vdrItemId, string? documentDescription, int? documentClass, DateTime? plannedFirstIssueDate, string? equipmentTag, string? disciplineCodeRaw, string? vendorFormatDocumentNumber, string? userName)
    {
        var r = await vdr.UpdateDetailsAsync(vdrItemId, new VdrItemEditInput(documentDescription, documentClass, plannedFirstIssueDate, equipmentTag, disciplineCodeRaw, vendorFormatDocumentNumber), userName);
        return r.Success ? (true, null) : (false, T(r.Error ?? "Vdr_Saved"));
    }

    private async Task<(bool Success, string? Error)> HoldStepAsync(string? reason, Func<Task> step)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, T("Vdr_Err_ReasonRequired"));
        try { await step(); return (true, null); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }      // the hold service's own rule texts (frozen, already on hold, ...)
    }

    public Task<(bool Success, string? Error)> HoldAsync(int vdrItemId, string? reason, int? actorEmployeeId) => HoldStepAsync(reason, () => hold.PlaceHoldAsync(vdrItemId, reason ?? "", null, actorEmployeeId));

    public Task<(bool Success, string? Error)> ReleaseHoldAsync(int vdrItemId, string? reason, int? actorEmployeeId) => HoldStepAsync(reason, () => hold.ReleaseHoldAsync(vdrItemId, reason ?? "", actorEmployeeId));

    public async Task<(bool Success, string? Error)> DeactivateAsync(int vdrItemId, string? reason, string? userName)
    {
        var r = await vdr.DeactivateAsync(vdrItemId, reason, userName);
        return r.Success ? (true, null) : (false, T(r.Error!));
    }

    public async Task<(bool Success, string? Error)> SetCommentActionAsync(int commentId, string actionStatus)
    {
        await comments.SetActionStatusAsync(commentId, actionStatus);
        return (true, null);
    }
}
