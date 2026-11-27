using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-838: the engineering document page as read operations over the services of EngineeringDocumentsController.Details (see <see cref="IEngDocDetailsOperations"/>).</summary>
public sealed class EngDocDetailsOperations(IEngineeringDocumentService documents, IDocumentWorkflowService workflow, IDocumentCommentService comments, IDocumentHoldService holds, ITransmittalService transmittals, ILetterService letters,
    IAccessControlService access, IPermissionService permissions, ICurrentUserService currentUser, PEMSDbContext context, IStringLocalizer<SharedResource> localizer) : IEngDocDetailsOperations
{
    private static readonly string[] StageOrder = ["Draft", "Submitted", "IDC Review", "Checked", "Approved", "Issued", "ClientReview"];
    private static readonly Dictionary<string, string> StageLabel = new() { ["Draft"] = "Draft", ["Submitted"] = "Check", ["IDC Review"] = "IDC", ["Checked"] = "Approval", ["Approved"] = "Issue", ["Issued"] = "Issued", ["ClientReview"] = "Client Review" };
    private static readonly Dictionary<string, string> StageRole = new() { ["Draft"] = "Engineer", ["Submitted"] = "Eng. Supervisor / Manager", ["IDC Review"] = "Referred disciplines", ["Checked"] = "Eng. Manager", ["Approved"] = "Document Controller", ["Issued"] = "-", ["ClientReview"] = "Document Controller" };

    private string StatusText(string? s) { var l = localizer[DocumentStatusBadge.ResourceKey(s)]; return l.ResourceNotFound ? (s ?? "") : l.Value; }
    private async Task<bool> Can(string action) => await permissions.HasPermissionAsync(currentUser.Roles, "EngineeringDocuments", action);

    private async Task<int?> EmployeeIdAsync() => currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;
    private async Task<int?> MyDisciplineIdAsync() { var e = await EmployeeIdAsync(); return e.HasValue ? await context.Employees.Where(x => x.EmployeeId == e.Value).Select(x => (int?)x.DisciplineId).FirstOrDefaultAsync() : null; }

    // the same visibility rule as the controller: a restricted user does not reach a document of another discipline by typing the address
    private async Task<EngineeringDocumentDto?> VisibleAsync(int id)
    {
        var d = await documents.GetByIdAsync(id);
        if (d == null) return null;
        if ((await access.AuthorizeForRolesAsync(currentUser.Roles, "EngineeringDocuments", "ViewAllDisciplines")).Allowed || !currentUser.UserId.HasValue) return d;
        var employeeId = await EmployeeIdAsync();
        if (!employeeId.HasValue) return d;
        var employee = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
        return employee == null || d.DisciplineId == employee.DisciplineId || d.DisciplineId == 0 ? d : null;
    }

    public async Task<List<EngDocHeadRow>> HeadAsync(int id)
    {
        var d = await VisibleAsync(id);
        if (d == null) return [];
        var idc = d.Status == "IDC Review" ? await workflow.GetIdcStatusAsync(id) : [];
        var mine = await MyDisciplineIdAsync();
        var myPending = idc.Any(x => x.DisciplineId == mine && x.Status == "Pending");
        string? next = d.Status switch
        {
            "Draft" => localizer["Ed_Next_Draft"].Value, "Submitted" => localizer["Ed_Next_Submitted"].Value, "IDC Review" => string.Format(localizer["Ed_Next_Idc"].Value, idc.Count(x => x.Status == "Pending")),
            "Checked" => localizer["Ed_Next_Checked"].Value, "Approved" => localizer["Ed_Next_Approved"].Value, "Issued" => string.IsNullOrEmpty(d.ClientApprovalStatus) ? localizer["Ed_Next_Issued"].Value : null,
            "Rejected" or "Revise & Resubmit" => localizer["Ed_Next_Revise"].Value, _ => null
        };
        var rejected = d.Status is "Rejected" or "Revise & Resubmit";
        var stageIdx = Array.IndexOf(StageOrder, d.Status);
        var hold = !d.IsOnHold && !d.IsFrozen && d.Status != "Issued" && await Can("Hold");
        var equipmentPid = (await context.Equipments.AsNoTracking().Where(x => x.IsActive && x.PIDNumber != null && x.ProjectId == d.ProjectId).Select(x => new { x.TagNumber, x.PIDNumber }).ToListAsync())
            .Where(x => PEMS.Shared.Search.DocumentNumberMatcher.IsMatch(x.PIDNumber, d.DocumentNo)).Select(x => x.TagNumber).ToList();
        return [new EngDocHeadRow(d.DocumentId, d.DocumentNo, d.Title, StatusText(d.Status), d.RevisionNo, d.IsFrozen, d.IsOnHold, d.ProjectName, d.DisciplineName, d.Area, d.DocumentType, d.CreatedDate, d.Description, d.IssuePurpose, d.PreparedBy,
            d.CheckedBy + (d.CheckedDate.HasValue ? $" ({d.CheckedDate:yyyy-MM-dd})" : ""), d.ApprovedBy + (d.ApprovedDate.HasValue ? $" ({d.ApprovedDate:yyyy-MM-dd})" : ""), d.CommentStatus,
            string.IsNullOrEmpty(d.ClientApprovalStatus) ? null : $"{d.ClientApprovalStatus} {d.ClientComment}".Trim(), d.EquipmentTagNumber, d.EquipmentId, d.ContractNo, d.ContractId, next,
            d.IsFrozen ? $"Revision {d.RevisionNo} is frozen and cannot be modified. Frozen by {d.FrozenByName ?? "-"} on {d.FrozenDate:yyyy-MM-dd}{(string.IsNullOrWhiteSpace(d.FreezeReason) ? "" : " - " + d.FreezeReason)}" : null,
            equipmentPid.Count == 0 ? null : string.Join(", ", equipmentPid), d.Status, rejected && stageIdx < 0 ? "Draft" : StageLabel.GetValueOrDefault(d.Status ?? "", d.Status ?? ""),
            d.Status is "Draft" or "Rejected" or "Revise & Resubmit", d.Status == "Submitted", d.Status is "Submitted" or "Checked", d.Status is "Submitted" or "Checked", d.Status == "IDC Review" && myPending,
            d.Status == "IDC Review" && !myPending ? "Waiting for the review of the other disciplines (IDC)" : null, d.Status == "Checked", d.Status == "Approved", d.Status == "Issued" && string.IsNullOrEmpty(d.ClientApprovalStatus), d.Status is "Approved" or "Issued",
            !d.IsFrozen && await Can("Freeze"), d.IsFrozen && await Can("Unfreeze"), hold, d.IsOnHold && await Can("ReleaseHold"), !d.IsFrozen && d.Status != "Issued", d.Status is "Rejected" or "Revise & Resubmit" ? "Resubmit" : "Submit For Review")];
    }

    public async Task<List<EngDocStageRow>> StagesAsync(int id)
    {
        var d = await VisibleAsync(id);
        if (d == null) return [];
        var status = d.Status ?? "Draft";
        var rejected = status is "Rejected" or "Revise & Resubmit";
        var current = Array.IndexOf(StageOrder, status);
        if (current < 0) current = rejected ? 0 : StageOrder.Length - 1;
        return StageOrder.Select((key, i) => new EngDocStageRow(key, rejected && i > current ? "rejected" : i < current ? "done" : i == current ? "current" : "upcoming", $"{StageLabel[key]} - {StageRole[key]}")).ToList();
    }

    public async Task<List<EngDocFileRow>> FilesAsync(int id)
        => await VisibleAsync(id) is { } d ? (d.Attachments ?? []).Select(a => new EngDocFileRow(a.AttachmentId, a.FileName, a.RevisionNo, a.FileRole, a.IsOfficial ? "Official" : "", $"{a.FileSize / 1024} KB", a.AIStatus, a.UploadDate, a.AIStatus == "Completed", string.IsNullOrEmpty(a.FileHash) ? "" : "SHA-256 " + a.FileHash[..Math.Min(8, a.FileHash.Length)])).ToList() : [];

    public async Task<List<EngDocIdcRow>> IdcAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await workflow.GetIdcStatusAsync(id)).Select(x => new EngDocIdcRow(x.DisciplineId, x.DisciplineName, x.Status, x.Comment, x.RespondedByName, x.RespondedDate)).ToList();

    public async Task<List<EngDocRevisionRow>> RevisionsAsync(int id)
        => await VisibleAsync(id) is { } d ? (d.Revisions ?? []).OrderByDescending(r => r.RevisionId).Select(r => new EngDocRevisionRow(r.RevisionId, r.RevisionNo, StatusText(r.Status), r.Reason, r.CreatedByName, r.ReviewerName, r.ApprovedBy, r.CreatedDate, r.IssuedDate, r.EffectiveDate, r.SupersededRevisionNo, r.ChangeDescription, r.VendorComment,
            string.IsNullOrEmpty(r.ClientReviewStatus) ? null : $"{r.ClientReviewStatus} {r.ClientReviewComment}".Trim())).ToList() : [];

    public async Task<List<EngDocHistoryRow>> HistoryAsync(int id)
    {
        if (await VisibleAsync(id) == null) return [];
        var n = 0;
        return (await workflow.GetWorkflowHistoryAsync(id)).OrderByDescending(h => h.ActionDate).Select(h => new EngDocHistoryRow(++n, h.ActionDate, h.ActionType, h.FromStageName, h.ToStageName, h.Decision, h.ActorName, h.Comment)).ToList();
    }

    public async Task<List<EngDocHoldRow>> HoldsAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await holds.GetHistoryAsync(id)).Select(h => new EngDocHoldRow(h.DocumentHoldId, h.Reason, h.HoldDate, h.HoldByName, h.ResponsibleDisciplineName, h.ReleaseDate, h.ReleaseByName, h.ReleaseReason)).ToList();

    public async Task<List<EngDocTransmittalRow>> TransmittalsAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await transmittals.GetForDocumentAsync(id)).Select(t => new EngDocTransmittalRow(t.TransmittalId, t.TransmittalNo, t.Status, t.TransmittalDate, t.SentDate, t.Purpose, t.RevisionNoAtTransmittal)).ToList();

    public async Task<List<EngDocLetterRow>> LettersAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await letters.GetRelatedToDocumentAsync(id)).Select(l => new EngDocLetterRow(l.LetterId, l.LetterType, l.LetterNumber, l.Subject, l.LetterDate, l.Status)).ToList();

    public async Task<List<EngDocCommentRow>> CommentsAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await comments.GetForDocumentAsync(id)).Select(c => new EngDocCommentRow(c.VDRCommentId, c.CommentNo, c.Category, c.Severity, $"{c.Sheet} {c.Zone}".Trim(), c.CommentText, c.RequiredAction, c.RaisedByName, c.RaisedDate, c.ActionStatus, c.ResponseText,
            string.IsNullOrEmpty(c.ResponseText) && string.IsNullOrEmpty(c.ReplyAttachmentFilePath), c.MarksRevisionRequired, c.AttachmentId.HasValue ? $"/Files/Download/{c.AttachmentId}" : null, c.ReplyAttachmentId.HasValue ? $"/Files/Download/{c.ReplyAttachmentId}" : null)).ToList();

    public async Task<List<EngDocPreviewRow>> PreviewAsync(int id)
    {
        var a = (await VisibleAsync(id))?.Attachments?.Where(x => x.FileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.UploadDate).FirstOrDefault();
        return a == null ? [] : [new EngDocPreviewRow(a.AttachmentId, $"/Files/Preview/{a.AttachmentId}", a.FileName)];
    }

    public async Task<List<EngDocPinRow>> PinsAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await comments.GetForDocumentAsync(id)).Where(c => c.PageNumber.HasValue).Select(c => new EngDocPinRow(c.VDRCommentId, c.PageNumber!.Value, c.PinX ?? 0, c.PinY ?? 0, c.CommentNo + ": " + c.CommentText)).ToList();

    public async Task<List<EngDocOption>> SubmitDisciplinesAsync(int id)
    {
        var d = await VisibleAsync(id);
        return d == null ? [] : await context.Disciplines.AsNoTracking().Where(x => x.IsActive && x.DisciplineId != d.DisciplineId).OrderBy(x => x.Name).Select(x => new EngDocOption(x.DisciplineId.ToString(), x.Name)).ToListAsync();
    }

    public async Task<List<EngDocOption>> AllDisciplinesAsync() => await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new EngDocOption(x.DisciplineId.ToString(), x.Name)).ToListAsync();

    public async Task<List<EngDocOption>> DepartmentsAsync() => await context.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new EngDocOption(x.DepartmentId.ToString(), x.Name)).ToListAsync();

    public async Task<List<EngDocOption>> IncomingLettersAsync(int id)
        => (await context.Letters.AsNoTracking().Where(l => l.IsActive && l.LetterType == "Incoming" && (l.RelatedDocumentId == null || l.RelatedDocumentId == id)).OrderByDescending(l => l.LetterDate).Take(30).ToListAsync())
            .Select(l => new EngDocOption(l.LetterId.ToString(), $"{l.LetterNumber} - {l.Subject} ({l.LetterDate:yyyy-MM-dd})")).ToList();

    public async Task<List<EngDocOption>> OpenCommentsAsync(int id)
        => await VisibleAsync(id) == null ? [] : (await comments.GetForDocumentAsync(id)).Where(c => c.MarksRevisionRequired && c.TriggeredRevisionId == null).Select(c => new EngDocOption(c.VDRCommentId.ToString(), c.CommentNo + " - " + c.CommentText)).ToList();

    public async Task<EngDocPinAnswer> AddPinAsync(int fileId, int page, double x, double y, string note, string? userName)
    {
        var documentId = await context.DocumentAttachments.AsNoTracking().Where(a => a.AttachmentId == fileId).Select(a => (int?)a.DocumentId).FirstOrDefaultAsync() ?? throw new InvalidOperationException("The file was not found.");
        return new EngDocPinAnswer(await comments.AddCommentAsync(documentId, note, userName, pageNumber: page, pinX: x, pinY: y));
    }
}
