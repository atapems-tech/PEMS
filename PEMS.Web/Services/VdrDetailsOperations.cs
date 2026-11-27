using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-830: the VDR item page as read operations over the services of VDRController.Details (see <see cref="IVdrDetailsOperations"/>).</summary>
public sealed class VdrDetailsOperations(IVDRService vdr, IDocumentCommentService comments, IVdrWorkflowService workflow, ILetterService letters, IWorkflowRuntime runtime, IPermissionService permissions, ICurrentUserService currentUser,
    PEMSDbContext context, IStringLocalizer<SharedResource> localizer) : IVdrDetailsOperations
{
    private static readonly string[] StageOrder = ["Planned", "Check", "DisciplineReview", "LeadReview", "ReturnToVendor", "Closed"];
    private static readonly Dictionary<string, string> StageRole = new() { ["Planned"] = "Vd_Role_Vendor", ["Check"] = "Vd_Role_Vendor", ["DisciplineReview"] = "Vd_Role_Disciplines", ["LeadReview"] = "Vd_Role_Lead", ["ReturnToVendor"] = "Vd_Role_DocControl" };

    private string Loc(string prefix, string? v) => VdrUi.Loc(localizer, prefix, v);
    private async Task<bool> Can(string action) => await permissions.HasPermissionAsync(currentUser.Roles, "VDR", action);

    private async Task<int?> MyDisciplineIdAsync()
    {
        if (!currentUser.UserId.HasValue) return null;
        var employeeId = await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        return employeeId.HasValue ? await context.Employees.Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DisciplineId).FirstOrDefaultAsync() : null;
    }

    private async Task<(string? Stage, bool Running, long InstanceId)> ActiveAsync(int id)
    {
        var a = await context.WorkflowInstances.Where(x => x.Module == "VendorDocument" && x.BusinessObjectType == "VDRItem" && x.BusinessObjectId == id).OrderByDescending(x => x.StartDate)
            .Select(x => new { x.Status, x.WorkflowInstanceId, StageName = x.CurrentStage != null ? x.CurrentStage.Name : null }).FirstOrDefaultAsync();
        return (a?.StageName, a != null && a.Status == "Running", a?.WorkflowInstanceId ?? 0);
    }

    public async Task<List<VdrHeadRow>> HeadAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        if (m == null) return [];
        var (stage, running, _) = await ActiveAsync(id);
        var canEdit = m.IsActive && await Can("Edit");
        var review = stage == "DisciplineReview" ? await workflow.GetDisciplineReviewStatusAsync(id) : [];
        var mine = await MyDisciplineIdAsync();
        var myPending = review.Any(x => x.DisciplineId == mine && x.Status == "Pending");
        return [new VdrHeadRow(m.VDRItemId, m.DocumentNumber, Loc("St_", m.WorkflowStatus), m.IsOverdue, m.LatestRevisionNo ?? 0, m.VendorName, m.VendorId, m.ProjectName, m.DocumentTypeCode, m.PoNumber, m.EquipmentTagNumber, m.EquipmentId,
            m.DisciplineCodeRaw + (m.IsMultiDiscipline ? " " + localizer["Vs_MultiDiscipline"].Value : ""), m.DocumentClass, m.PlannedFirstIssueDate, m.VendorFormatDocumentNumber, m.DocumentDescription, m.IsOnHold, m.HoldReason, stage,
            canEdit, m.IsActive && !m.IsOnHold && await Can("Hold"), m.IsOnHold && await Can("ReleaseHold"), m.IsActive && await Can("Delete"),
            !running, running && stage == "DisciplineReview" && myPending, running && stage == "DisciplineReview" && !myPending ? localizer["Vd_WaitingOthers"].Value : null, running && stage == "LeadReview", running && stage == "ReturnToVendor",
            (m.LatestRevisionNo ?? -1) + 1, m.Submissions.Any(), m.PlannedFirstIssueDate?.ToString("yyyy-MM-dd") ?? "", m.DocumentDescription ?? "", m.DocumentClass?.ToString() ?? "", m.EquipmentTagNumber ?? "", m.DisciplineCodeRaw ?? "")];
    }

    public async Task<List<VdrStageRow>> StagesAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        if (m == null) return [];
        var status = m.WorkflowStatus ?? "Planned";
        var (stageName, _, _) = await ActiveAsync(id);
        var pending = status == "PendingResubmission";
        int current;
        if (!string.IsNullOrEmpty(stageName) && Array.IndexOf(StageOrder, stageName) >= 0) current = Array.IndexOf(StageOrder, stageName);
        else if (status == "Closed") current = StageOrder.Length - 1;
        else if (pending) current = Array.IndexOf(StageOrder, "LeadReview");
        else current = 0;
        return StageOrder.Select((key, i) =>
        {
            var branched = pending && i >= current && key != "Planned" && key != "Check";
            var step = branched ? (i == current ? "rejected" : "upcoming") : i < current ? "done" : i == current ? "current" : "upcoming";
            var role = StageRole.TryGetValue(key, out var r) ? localizer[r].Value : "—";
            return new VdrStageRow(key, step, localizer["Vd_Stage_" + key].Value + " - " + role);
        }).ToList();
    }

    public async Task<List<VdrDisciplineReviewRow>> ReviewAsync(int id)
        => (await workflow.GetDisciplineReviewStatusAsync(id)).Select(r => new VdrDisciplineReviewRow(r.DisciplineId, r.DisciplineName, Loc("St_", r.Status))).ToList();

    public async Task<List<VdrSubmissionRow>> SubmissionsAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        if (m == null) return [];
        return m.Submissions.OrderBy(s => s.RevisionNo).Select(s => new VdrSubmissionRow(s.VDRSubmissionId, s.RevisionNo, s.VendorTransmittalNo, s.ClientTransmittalNo, s.VendorActualIssuedDate, s.PurposeOfIssueCode,
            s.ClientCommentDate == null && s.ContractorCommentDueDate.HasValue ? $"{localizer["Vd_ContractorDue"].Value} {s.ContractorCommentDueDate:yyyy-MM-dd}{(s.IsContractorCommentOverdue ? " " + localizer["Vd_OverdueParen"].Value : "")}"
                : s.ReplySheetDate == null && s.VendorReturnDueDate.HasValue ? $"{localizer["Vd_ReturnDue"].Value} {s.VendorReturnDueDate:yyyy-MM-dd}{(s.IsVendorReturnOverdue ? " " + localizer["Vd_OverdueParen"].Value : "")}" : null,
            s.FtpUploadedDate.HasValue ? s.FtpUploadedDate.Value.ToString("yyyy-MM-dd") : null, s.ClientCommentSheetNo, s.ClientCommentDate, s.ReviewCategory, s.RequiresResubmission,
            string.IsNullOrEmpty(s.CommentSheetFilePath), string.IsNullOrEmpty(s.ReplySheetFilePath), s.CommentSheetAttachmentId, s.ReplySheetAttachmentId)).ToList();
    }

    public async Task<List<VdrFileRow>> FilesAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        if (m == null) return [];
        return VdrFiles.From(m.Submissions).Select(f => new VdrFileRow(f.AttachmentId, f.FileName, f.RevisionNo, localizer["Vd_FileKind_" + f.Kind].Value, $"/Files/Preview/{f.AttachmentId}", $"/Files/Download/{f.AttachmentId}", f.CanPreview)).ToList();
    }

    public async Task<List<VdrCommentRow>> CommentsAsync(int id)
        => (await comments.GetForDocumentAsync(id)).Select(c => new VdrCommentRow(c.VDRCommentId, c.CommentNo, c.RevisionNo,
            string.IsNullOrEmpty(c.Category) ? null : Loc("Vd_Cat_", c.Category), string.IsNullOrEmpty(c.Severity) ? null : Loc("Vd_Sev_", c.Severity), $"{c.Sheet} {c.Zone}".Trim(), c.CommentText, c.RequiredAction, c.RaisedByName, c.RaisedDate, Loc("St_", c.ActionStatus), c.ResponseText,
            string.IsNullOrEmpty(c.ResponseText) && string.IsNullOrEmpty(c.ReplyAttachmentFilePath), c.AttachmentId.HasValue ? $"/Files/Download/{c.AttachmentId}" : null, c.ReplyAttachmentId.HasValue ? $"/Files/Download/{c.ReplyAttachmentId}" : null)).ToList();

    public async Task<List<VdrHistoryRow>> HistoryAsync(int id)
    {
        var (_, _, instance) = await ActiveAsync(id);
        if (instance == 0) return [];
        var n = 0;
        return (await runtime.GetHistoryAsync((int)instance)).OrderByDescending(h => h.ActionDate).Select(h => new VdrHistoryRow(++n, h.ActionDate, h.ActionType, string.IsNullOrEmpty(h.FromStageName) ? null : Loc("Vd_Stage_", h.FromStageName), string.IsNullOrEmpty(h.ToStageName) ? null : Loc("Vd_Stage_", h.ToStageName), h.Decision, h.Comment)).ToList();
    }

    public async Task<List<VdrLetterRow>> LettersAsync(int id)
        => (await letters.GetRelatedToDocumentAsync(id)).Select(l => new VdrLetterRow(l.LetterId, l.LetterType, l.LetterNumber, l.Subject, l.LetterDate, l.Status)).ToList();

    public async Task<List<VdrPreviewRow>> PreviewAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        var s = m?.Submissions.Where(x => x.AttachmentId.HasValue).OrderByDescending(x => x.RevisionNo).FirstOrDefault();
        return s == null ? [] : [new VdrPreviewRow(s.AttachmentId!.Value, $"/Files/Preview/{s.AttachmentId}", Path.GetFileName(s.MainFilePath ?? ""))];
    }

    public async Task<List<VdrPinRow>> PinsAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        var s = m?.Submissions.Where(x => x.AttachmentId.HasValue).OrderByDescending(x => x.RevisionNo).FirstOrDefault();
        if (s == null) return [];
        return (await comments.GetForDocumentAsync(id)).Where(c => c.VDRSubmissionId == s.VDRSubmissionId && c.PageNumber.HasValue)
            .Select(c => new VdrPinRow(c.VDRCommentId, c.PageNumber!.Value, c.PinX ?? 0, c.PinY ?? 0, c.CommentNo + ": " + c.CommentText)).ToList();
    }

    public async Task<List<VdrChoice>> SubmissionOptionsAsync(int id)
    {
        var m = await vdr.GetByIdAsync(id);
        return m == null ? [] : m.Submissions.OrderByDescending(s => s.RevisionNo).Select(s => new VdrChoice(s.VDRSubmissionId.ToString(), $"{localizer["Vs_Rev"].Value} {s.RevisionNo}" + (string.IsNullOrEmpty(s.VendorTransmittalNo) ? "" : " - " + s.VendorTransmittalNo))).ToList();
    }

    public async Task<List<VdrChoice>> DisciplinesAsync() => await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new VdrChoice(x.DisciplineId.ToString(), x.Name)).ToListAsync();

    public async Task<VdrPinAnswer> AddPinAsync(int fileId, int page, double x, double y, string note, string? userName)
    {
        var submission = await context.VDRSubmissions.AsNoTracking().Where(s => s.AttachmentId == fileId).Select(s => new { s.VDRSubmissionId, s.VDRItemId }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The file is not the main file of a submission.");
        var commentId = await comments.AddCommentAsync(submission.VDRItemId, note, userName, submission.VDRSubmissionId, pageNumber: page, pinX: x, pinY: y);
        return new VdrPinAnswer(commentId);
    }
}
