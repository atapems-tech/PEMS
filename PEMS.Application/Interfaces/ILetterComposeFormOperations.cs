namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-840: the starting values of the compose form: one flat row (the lists of employees and approvers are comma separated ids), read for a new letter, a draft (id), a reply (replyTo), a referral (referTo) or a template (templateId).</summary>
public sealed record LetterComposeFormRow(int? LetterId, string RowVersion, int? ParentLetterId, string IsReferral, string LetterType, string Category, string Subject, string Body, string Priority, string ConfidentialityLevel,
    DateTime LetterDate, string ReferenceNumber, int? ProjectId, int? DisciplineId, int? RelatedDepartmentId, int? SenderDepartmentId, string ToEmployeeIds, string CcEmployeeIds, string BccEmployeeIds,
    string ExternalReceiver, int? CorrespondentPartyId, string ResponseRequired, DateTime? ResponseDueDate, string ApprovalRequired, string ApproverUserIds, string ManualNumber);

/// <summary>The answer of a compose command: NewId is the letter id (for the redirect); Error is the translated refusal (all the errors of the check, one per line).</summary>
public sealed record LetterComposeCommandResult(bool Success, string? Error, int NewId);

/// <summary>The starting values of the register-an-incoming-letter form (a template fills the subject, the body, the priority and the confidentiality).</summary>
public sealed record LetterIncomingFormRow(DateTime LetterDate, string Subject, string Body, string Priority, string ConfidentialityLevel);

/// <summary>The answer of the editor image upload: NewId is the address of the stored picture (the script reads it as the result).</summary>
public sealed record LetterEditorImageResult(bool Success, string? Error, string NewId);

public sealed record LetterVersionRow(int Id, int VersionNo, string Reason, string SavedBy, DateTime Saved, string Subject);
public sealed record LetterViewedRow(int Id, string UserName, DateTime FirstViewed, DateTime LastViewed, int ViewCount);
public sealed record LetterAuditRow(int Id, DateTime When, string Actor, string Category, string Event, string Detail);
public sealed record LetterHistoryHeadRow(int Id, string Number, string Subject, string Status, string Confidentiality);

/// <summary>
/// SCRUM-840: what LetterComposeController did, as operations: the starting values of the compose form, the two commands (save the draft, submit for approval or send), the choices of the form, the history of a letter (who viewed,
/// the versions, the audit trail) and its compliance CSV. The rights are the old ones (Correspondence.*, the author of a draft, the auditors of a letter); the preview is the letter PDF of the PDF engine.
/// </summary>
public interface ILetterComposeFormOperations
{
    [PEMS.Application.Common.PemsOperation("LetterCompose", "Record", "Correspondence.Create")]
    Task<List<LetterComposeFormRow>> RecordAsync(int? id = null, string? letterType = null, int? templateId = null, int? replyTo = null, int? referTo = null);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Save", "Correspondence.Create")]
    Task<LetterComposeCommandResult> SaveAsync(int? letterId, string? rowVersion, int? parentLetterId, string? isReferral, string? letterType, string? category, string? subject, string? body, string? priority,
        string? confidentialityLevel, DateTime? letterDate, string? referenceNumber, int? projectId, int? disciplineId, int? relatedDepartmentId, int? senderDepartmentId, string? toEmployeeIds, string? ccEmployeeIds,
        string? bccEmployeeIds, string? externalReceiver, int? correspondentPartyId, string? responseRequired, DateTime? responseDueDate, string? approvalRequired, string? approverUserIds, string? manualNumber);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Submit", "Correspondence.Create")]
    Task<LetterComposeCommandResult> SubmitAsync(int? letterId, string? rowVersion, int? parentLetterId, string? isReferral, string? letterType, string? category, string? subject, string? body, string? priority,
        string? confidentialityLevel, DateTime? letterDate, string? referenceNumber, int? projectId, int? disciplineId, int? relatedDepartmentId, int? senderDepartmentId, string? toEmployeeIds, string? ccEmployeeIds,
        string? bccEmployeeIds, string? externalReceiver, int? correspondentPartyId, string? responseRequired, DateTime? responseDueDate, string? approvalRequired, string? approverUserIds, string? manualNumber);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Employees", "Correspondence.Create")]
    Task<List<LetterOption>> EmployeesAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Approvers", "Correspondence.Create")]
    Task<List<LetterOption>> ApproversAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Projects", "Correspondence.Create")]
    Task<List<LetterOption>> ProjectsAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Disciplines", "Correspondence.Create")]
    Task<List<LetterOption>> DisciplinesAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Departments", "Correspondence.Create")]
    Task<List<LetterOption>> DepartmentsAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Parties", "Correspondence.Create")]
    Task<List<LetterOption>> PartiesAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "HistoryHead", "Correspondence.View")]
    Task<List<LetterHistoryHeadRow>> HistoryHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Viewed", "Correspondence.View")]
    Task<List<LetterViewedRow>> ViewedAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Versions", "Correspondence.View")]
    Task<List<LetterVersionRow>> VersionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Audit", "Correspondence.View")]
    Task<List<LetterAuditRow>> AuditAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "ComplianceCsv", "Correspondence.View")]
    Task<BarcodeFileResult> ComplianceCsvAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "IncomingRecord", "Correspondence.Create")]
    Task<List<LetterIncomingFormRow>> IncomingRecordAsync(int? templateId = null);

    /// <summary>Registers an incoming letter (the number is generated unless the user may and wants to type it).</summary>
    [PEMS.Application.Common.PemsOperation("LetterCompose", "RegisterIncoming", "Correspondence.Create")]
    Task<LetterComposeCommandResult> RegisterIncomingAsync(string? letterNumber, DateTime? letterDate, string? sender, string? subject, string? body, string? priority, string? confidentialityLevel, int? projectId, int? disciplineId,
        int? relatedDocumentId, int? relatedMRId, string? useManualNumber, string? responseRequired, DateTime? responseDueDate);

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Documents", "Correspondence.Create")]
    Task<List<LetterOption>> DocumentsAsync();

    [PEMS.Application.Common.PemsOperation("LetterCompose", "Requisitions", "Correspondence.Create")]
    Task<List<LetterOption>> RequisitionsAsync();

    /// <summary>Stores a picture chosen in the rich text editor of a letter (type, size and content are checked by the upload policy of the action) and answers its address.</summary>
    [PEMS.Application.Common.PemsOperation("LetterCompose", "EditorImage", "Correspondence.Create")]
    Task<LetterEditorImageResult> EditorImageAsync(PEMS.Application.Common.OperationFile? file);
}
