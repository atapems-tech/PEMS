using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-840: what LetterComposeController did (see <see cref="ILetterComposeFormOperations"/>): the same services, the same rules and the same refusals; the page is the form definition <c>letter-compose</c>.</summary>
public sealed class LetterComposeFormOperations(ILetterComposeService compose, ILetterService letters, ILetterComplianceService compliance, IPermissionService permissions, ICurrentUserService currentUser,
    PEMSDbContext context, IStringLocalizer<SharedResource> localizer, ILetterNumberingService numbering, PEMS.Application.Uploads.IProtectedImageStore images, ILetterTemplateService? templates = null) : ILetterComposeFormOperations
{
    private bool IsAdmin => currentUser.Roles.Contains("SystemAdmin");
    private async Task<bool> CanSeeAllAsync() => IsAdmin || await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    private async Task<LetterComposeActor?> ActorAsync()
    {
        if (!currentUser.UserId.HasValue) return null;
        var userId = currentUser.UserId.Value;
        var employeeId = await context.Users.Where(u => u.Id == userId).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        var manual = await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "ManualNumber");
        return new LetterComposeActor(userId, currentUser.UserName, employeeId, manual, IsAdmin);
    }

    private async Task<LetterViewer> ViewerAsync()
    {
        var userId = currentUser.UserId;
        int? employeeId = userId.HasValue ? await context.Users.Where(u => u.Id == userId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;
        return new LetterViewer(userId, employeeId, await CanSeeAllAsync(), currentUser.UserName);
    }

    // the versions and the compliance report are more sensitive than reading the letter: its author or whoever sees all letters
    private async Task<bool> CanAuditAsync(int letterId)
    {
        if (!currentUser.UserId.HasValue) return false;
        if (await CanSeeAllAsync()) return true;
        var registeredBy = await context.Letters.Where(l => l.LetterId == letterId).Select(l => (int?)l.RegisteredById).FirstOrDefaultAsync();
        return registeredBy == currentUser.UserId.Value;
    }

    private string Translate(LetterComposeIssue issue, bool warning)
    {
        var text = localizer[(warning ? "Lc_Warn_" : "Lc_Err_") + issue.Code];
        var message = text.ResourceNotFound ? issue.Code : text.Value;
        var detail = issue.Detail ?? "";
        if (issue.Code == "ConfidentialityBelowParent" && localizer["Lt_C_" + detail] is { ResourceNotFound: false } level) detail = level.Value;
        return message.Replace("{0}", detail).Trim();
    }

    private static List<int> Ids(string? text) => (text ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();
    private static string Join(IEnumerable<int> ids) => string.Join(",", ids);
    private static bool Flag(string? s) => s is not null && (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" || s.Equals("on", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string> TemplateValues(string letterType, LetterSenderBlockDto sender) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"), ["UserName"] = sender.FullName, ["LetterType"] = letterType, ["SenderName"] = sender.FullName,
        ["SenderTitle"] = sender.Title ?? "", ["SenderDepartment"] = sender.DepartmentName ?? ""
    };

    public async Task<List<LetterComposeFormRow>> RecordAsync(int? id = null, string? letterType = null, int? templateId = null, int? replyTo = null, int? referTo = null)
    {
        var actor = await ActorAsync();
        if (actor is null) return [];
        var type = !string.IsNullOrWhiteSpace(letterType) && LetterComposeRules.ComposableTypes.Contains(letterType) ? letterType : "Internal";

        LetterComposeInput input;
        if (id.HasValue)
        {
            if (await compose.LoadForEditAsync(id.Value, actor) is not { } loaded) return [];
            input = loaded;
        }
        else if (referTo.HasValue)
        {
            if (!await letters.CanViewAsync(referTo.Value, await ViewerAsync())) throw new UnauthorizedAccessException();
            if (await compose.BuildReferralInputAsync(referTo.Value, actor) is not { } referral) return [];
            input = referral;
        }
        else if (replyTo.HasValue)
        {
            if (!await letters.CanViewAsync(replyTo.Value, await ViewerAsync())) throw new UnauthorizedAccessException();
            if (await compose.BuildReplyInputAsync(replyTo.Value, actor) is not { } reply) return [];
            input = reply;
        }
        else input = new LetterComposeInput { LetterType = type, ApprovalRequired = type == "Outgoing" };

        var sender = await compose.GetSenderBlockAsync(actor.UserId);
        if (!id.HasValue) input.SenderDepartmentId ??= sender.DepartmentId;

        if (templateId.HasValue && !id.HasValue && templates is not null && await templates.GetAsync(templateId.Value) is { IsActive: true } template && (template.LetterType == null || template.LetterType == input.LetterType))
        {
            var values = TemplateValues(input.LetterType, sender);
            if (input.ParentLetterId.HasValue)
            {
                var parent = await context.Letters.AsNoTracking().Where(l => l.LetterId == input.ParentLetterId.Value).Select(l => new { l.LetterNumber, l.Subject, l.LetterDate, l.ProjectId, l.Sender }).FirstOrDefaultAsync();
                if (parent is not null)
                {
                    values["ParentNumber"] = parent.LetterNumber;
                    values["ParentSubject"] = parent.Subject;
                    values["ParentDate"] = parent.LetterDate.ToString("yyyy-MM-dd");
                    values["Counterparty"] = parent.Sender ?? "";
                    values["ParentProject"] = await context.Projects.Where(x => x.ProjectId == parent.ProjectId).Select(x => x.ProjectName).FirstOrDefaultAsync() ?? "";
                }
            }
            input.Subject = LetterTemplatePlaceholders.Apply(template.Subject, values, htmlEncode: false);
            input.Body = LetterTemplatePlaceholders.Apply(template.Body, values, htmlEncode: true);
            input.Priority = template.Priority;
            input.ConfidentialityLevel = template.ConfidentialityLevel;
        }

        return [new LetterComposeFormRow(input.LetterId, input.RowVersion is null ? "" : Convert.ToBase64String(input.RowVersion), input.ParentLetterId, input.IsReferral ? "true" : "false", input.LetterType, input.Category ?? "", input.Subject, input.Body ?? "",
            input.Priority, input.ConfidentialityLevel, input.LetterDate, input.ReferenceNumber ?? "", input.ProjectId, input.DisciplineId, input.RelatedDepartmentId, input.SenderDepartmentId, Join(input.ToEmployeeIds), Join(input.CcEmployeeIds),
            Join(input.BccEmployeeIds), input.ExternalReceiver ?? "", input.CorrespondentPartyId, input.ResponseRequired ? "true" : "false", input.ResponseDueDate, input.ApprovalRequired ? "true" : "false", Join(input.ApproverUserIds), input.ManualNumber ?? "")];
    }

    private static LetterComposeInput InputOf(int? letterId, string? rowVersion, int? parentLetterId, string? isReferral, string? letterType, string? category, string? subject, string? body, string? priority,
        string? confidentialityLevel, DateTime? letterDate, string? referenceNumber, int? projectId, int? disciplineId, int? relatedDepartmentId, int? senderDepartmentId, string? toEmployeeIds, string? ccEmployeeIds,
        string? bccEmployeeIds, string? externalReceiver, int? correspondentPartyId, string? responseRequired, DateTime? responseDueDate, string? approvalRequired, string? approverUserIds, string? manualNumber)
    {
        var input = new LetterComposeInput
        {
            LetterId = letterId, ParentLetterId = parentLetterId, IsReferral = Flag(isReferral), LetterType = string.IsNullOrWhiteSpace(letterType) ? "Internal" : letterType, Category = string.IsNullOrWhiteSpace(category) ? null : category,
            Subject = subject ?? "", Body = body, Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority, ConfidentialityLevel = string.IsNullOrWhiteSpace(confidentialityLevel) ? "Internal" : confidentialityLevel,
            LetterDate = letterDate ?? DateTime.Today, ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber, ProjectId = projectId, DisciplineId = disciplineId, RelatedDepartmentId = relatedDepartmentId,
            SenderDepartmentId = senderDepartmentId, ToEmployeeIds = Ids(toEmployeeIds), CcEmployeeIds = Ids(ccEmployeeIds), BccEmployeeIds = Ids(bccEmployeeIds), ExternalReceiver = string.IsNullOrWhiteSpace(externalReceiver) ? null : externalReceiver,
            CorrespondentPartyId = correspondentPartyId, ResponseRequired = Flag(responseRequired), ResponseDueDate = responseDueDate, ApprovalRequired = Flag(approvalRequired), ApproverUserIds = Ids(approverUserIds),
            ManualNumber = string.IsNullOrWhiteSpace(manualNumber) ? null : manualNumber
        };
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            try { input.RowVersion = Convert.FromBase64String(rowVersion); }
            catch (FormatException) { input.RowVersion = null; }
        }
        return input;
    }

    private LetterComposeCommandResult Refused(LetterComposeResult r) => new(false, string.Join("\n", r.Errors.Select(e => Translate(e, false))), r.LetterId);

    public async Task<LetterComposeCommandResult> SaveAsync(int? letterId, string? rowVersion, int? parentLetterId, string? isReferral, string? letterType, string? category, string? subject, string? body, string? priority,
        string? confidentialityLevel, DateTime? letterDate, string? referenceNumber, int? projectId, int? disciplineId, int? relatedDepartmentId, int? senderDepartmentId, string? toEmployeeIds, string? ccEmployeeIds,
        string? bccEmployeeIds, string? externalReceiver, int? correspondentPartyId, string? responseRequired, DateTime? responseDueDate, string? approvalRequired, string? approverUserIds, string? manualNumber)
    {
        if (await ActorAsync() is not { } actor) return new(false, "Sign in first.", 0);
        var result = await compose.SaveDraftAsync(InputOf(letterId, rowVersion, parentLetterId, isReferral, letterType, category, subject, body, priority, confidentialityLevel, letterDate, referenceNumber, projectId, disciplineId,
            relatedDepartmentId, senderDepartmentId, toEmployeeIds, ccEmployeeIds, bccEmployeeIds, externalReceiver, correspondentPartyId, responseRequired, responseDueDate, approvalRequired, approverUserIds, manualNumber), actor, autoSave: false);
        return result.Success ? new(true, null, result.LetterId) : Refused(result);
    }

    public async Task<LetterComposeCommandResult> SubmitAsync(int? letterId, string? rowVersion, int? parentLetterId, string? isReferral, string? letterType, string? category, string? subject, string? body, string? priority,
        string? confidentialityLevel, DateTime? letterDate, string? referenceNumber, int? projectId, int? disciplineId, int? relatedDepartmentId, int? senderDepartmentId, string? toEmployeeIds, string? ccEmployeeIds,
        string? bccEmployeeIds, string? externalReceiver, int? correspondentPartyId, string? responseRequired, DateTime? responseDueDate, string? approvalRequired, string? approverUserIds, string? manualNumber)
    {
        if (await ActorAsync() is not { } actor) return new(false, "Sign in first.", 0);
        var input = InputOf(letterId, rowVersion, parentLetterId, isReferral, letterType, category, subject, body, priority, confidentialityLevel, letterDate, referenceNumber, projectId, disciplineId,
            relatedDepartmentId, senderDepartmentId, toEmployeeIds, ccEmployeeIds, bccEmployeeIds, externalReceiver, correspondentPartyId, responseRequired, responseDueDate, approvalRequired, approverUserIds, manualNumber);
        var saved = await compose.SaveDraftAsync(input, actor, autoSave: false);
        if (!saved.Success) return Refused(saved);

        var check = await compose.ValidateForSubmitAsync(saved.LetterId, actor);   // the full check on the saved version
        if (!check.Success) return new(false, string.Join("\n", check.Errors.Select(e => Translate(e, false))), saved.LetterId);

        var (ok, error) = input.ApprovalRequired ? await letters.SubmitForReviewAsync(saved.LetterId, actor.UserId) : await letters.SendAsync(saved.LetterId, actor.UserId);
        return ok ? new(true, null, saved.LetterId) : new(false, error ?? "The letter could not be submitted.", saved.LetterId);
    }

    public async Task<List<LetterOption>> EmployeesAsync()
        => (await context.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.LastName).ThenBy(e => e.FirstName).Take(3000).Select(e => new { e.EmployeeId, e.FirstName, e.LastName }).ToListAsync())
            .Select(e => new LetterOption(e.EmployeeId.ToString(CultureInfo.InvariantCulture), (e.FirstName + " " + e.LastName).Trim())).ToList();

    public async Task<List<LetterOption>> ApproversAsync()
        => (await compose.GetApproverOptionsAsync(currentUser.UserId)).Select(a => new LetterOption(a.UserId.ToString(CultureInfo.InvariantCulture), a.FullName + (string.IsNullOrWhiteSpace(a.Position) ? "" : " - " + a.Position))).ToList();

    public async Task<List<LetterOption>> ProjectsAsync()
        => (await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync()).Select(x => new LetterOption(x.ProjectId.ToString(CultureInfo.InvariantCulture), x.ProjectName)).ToList();

    public async Task<List<LetterOption>> DisciplinesAsync()
        => (await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.DisciplineId, x.Name }).ToListAsync()).Select(x => new LetterOption(x.DisciplineId.ToString(CultureInfo.InvariantCulture), x.Name)).ToList();

    public async Task<List<LetterOption>> DepartmentsAsync()
        => (await context.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.DepartmentId, x.Name }).ToListAsync()).Select(x => new LetterOption(x.DepartmentId.ToString(CultureInfo.InvariantCulture), x.Name)).ToList();

    public async Task<List<LetterOption>> PartiesAsync()
        => (await context.CorrespondentParties.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.CorrespondentPartyId, x.Name }).ToListAsync()).Select(x => new LetterOption(x.CorrespondentPartyId.ToString(CultureInfo.InvariantCulture), x.Name)).ToList();

    // ------------------------------------------------------------------ history

    private async Task<bool> MayAuditAsync(int id) => await letters.CanViewAsync(id, await ViewerAsync()) && await CanAuditAsync(id);

    public async Task<List<LetterHistoryHeadRow>> HistoryHeadAsync(int id)
    {
        if (!await MayAuditAsync(id)) return [];
        var report = await compliance.BuildComplianceReportAsync(id);
        return report is null ? [] : [new LetterHistoryHeadRow(report.LetterId, report.LetterNumber, report.Subject, report.Status, report.ConfidentialityLevel)];
    }

    public async Task<List<LetterViewedRow>> ViewedAsync(int id)
        => !await MayAuditAsync(id) ? [] : (await compliance.GetViewLogAsync(id)).Select((v, i) => new LetterViewedRow(i + 1, v.UserName, v.FirstViewedDate, v.LastViewedDate, v.ViewCount)).ToList();

    public async Task<List<LetterVersionRow>> VersionsAsync(int id)
        => !await MayAuditAsync(id) ? [] : (await compose.GetVersionsAsync(id)).OrderByDescending(v => v.VersionNo).Select(v => new LetterVersionRow(v.VersionNo, v.VersionNo, v.Reason, v.SavedByName ?? "", v.SavedDate, v.Subject)).ToList();

    public async Task<List<LetterAuditRow>> AuditAsync(int id)
    {
        if (!await MayAuditAsync(id)) return [];
        var report = await compliance.BuildComplianceReportAsync(id);
        return report is null ? [] : report.Rows.Select((r, i) => new LetterAuditRow(i + 1, r.Timestamp, r.Actor, r.Category, r.Event, r.Detail)).ToList();
    }

    // a cell that starts with = + - @ would run as a formula in Excel
    private static string Csv(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public async Task<BarcodeFileResult> ComplianceCsvAsync(int id)
    {
        if (!await MayAuditAsync(id) || await compliance.BuildComplianceReportAsync(id) is not { } report) return new(false, "Not found.", [], "", "text/plain");
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Actor,Category,Event,Detail");
        foreach (var row in report.Rows)
            sb.AppendLine(string.Join(",", Csv(row.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")), Csv(row.Actor), Csv(row.Category), Csv(row.Event), Csv(row.Detail)));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();   // a BOM so Excel reads Persian text
        return new(true, null, bytes, $"{report.LetterNumber}-compliance.csv", "text/csv");
    }

    // ------------------------------------------------------------------ register an incoming letter, the editor image

    public async Task<List<LetterIncomingFormRow>> IncomingRecordAsync(int? templateId = null)
    {
        var row = new LetterIncomingFormRow(DateTime.Today, "", "", "Normal", "Internal");
        if (templateId.HasValue && templates is not null && await templates.GetAsync(templateId.Value) is { IsActive: true } t && (t.LetterType == null || t.LetterType == "Incoming"))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"), ["UserName"] = currentUser.FullName ?? currentUser.UserName ?? "", ["LetterType"] = "Incoming" };
            row = row with { Subject = LetterTemplatePlaceholders.Apply(t.Subject, values, htmlEncode: false), Body = LetterTemplatePlaceholders.Apply(t.Body, values, htmlEncode: true), Priority = t.Priority, ConfidentialityLevel = t.ConfidentialityLevel };
        }
        return [row];
    }

    private async Task<int?> DepartmentIdAsync()
    {
        if (!currentUser.UserId.HasValue) return null;
        var userId = currentUser.UserId.Value;
        var employeeId = await context.Users.Where(u => u.Id == userId).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        return employeeId.HasValue ? await context.Employees.Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync() : null;
    }

    public async Task<LetterComposeCommandResult> RegisterIncomingAsync(string? letterNumber, DateTime? letterDate, string? sender, string? subject, string? body, string? priority, string? confidentialityLevel, int? projectId, int? disciplineId,
        int? relatedDocumentId, int? relatedMRId, string? useManualNumber, string? responseRequired, DateTime? responseDueDate)
    {
        var date = letterDate ?? DateTime.Today;
        var respond = Flag(responseRequired);
        if (string.IsNullOrWhiteSpace(subject)) return new(false, "The subject is required.", 0);
        if (string.IsNullOrWhiteSpace(sender)) return new(false, "The sender is required.", 0);
        if (LetterResponseRules.Validate(respond, responseDueDate, date) is { } responseError) return new(false, responseError, 0);   // before a number is taken

        var canManual = await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "ManualNumber");
        var number = Flag(useManualNumber) && canManual && !string.IsNullOrWhiteSpace(letterNumber) ? letterNumber : await numbering.GenerateNextNumberAsync("Incoming", projectId, await DepartmentIdAsync());
        var id = await letters.CreateAsync("Incoming", number, date, sender, null, subject, body, string.IsNullOrWhiteSpace(priority) ? "Normal" : priority, string.IsNullOrWhiteSpace(confidentialityLevel) ? "Internal" : confidentialityLevel,
            projectId, disciplineId, relatedDocumentId, relatedMRId, currentUser.UserId, respond, responseDueDate);
        return new(true, null, id);
    }

    public async Task<List<LetterOption>> DocumentsAsync()
        => (await context.Documents.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.CreatedDate).Take(200).Select(x => new { x.DocumentId, x.DocumentNo, x.Title }).ToListAsync())
            .Select(x => new LetterOption(x.DocumentId.ToString(CultureInfo.InvariantCulture), x.DocumentNo + " - " + x.Title)).ToList();

    public async Task<List<LetterOption>> RequisitionsAsync()
        => (await context.MaterialRequisitions.AsNoTracking().OrderByDescending(x => x.CreatedDate).Take(200).Select(x => new { x.MRId, x.MRNumber }).ToListAsync())
            .Select(x => new LetterOption(x.MRId.ToString(CultureInfo.InvariantCulture), x.MRNumber)).ToList();

    public async Task<LetterEditorImageResult> EditorImageAsync(OperationFile? file)
    {
        if (file is null || file.Bytes.Length == 0) return new(false, "Choose a picture.", "");
        await using var stream = new MemoryStream(file.Bytes);
        return new(true, null, await images.SaveAsync(PEMS.Application.Uploads.ProtectedImageStore.LetterImages, file.FileName, stream));
    }
}
