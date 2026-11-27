namespace PEMS.Application.Interfaces;

public sealed record VdrHeadRow(int Id, string DocumentNumber, string Status, bool Overdue, int Revision, string Vendor, int VendorId, string Project, string? Type, string? Po, string? Equipment, int? EquipmentId, string? Discipline, int? Class, DateTime? PlannedFirstIssue,
    string? VendorFormatNo, string? Description, bool OnHold, string? HoldReason, string? Stage, bool CanEdit, bool CanHold, bool CanRelease, bool CanDeactivate, bool CanStart, bool CanRespond, string? Waiting, bool CanLead, bool CanReturn, int NextRevision,
    bool CanAddComment, string PlannedIso, string EditDescription, string EditClass, string EditEquipment, string EditDiscipline);
public sealed record VdrStageRow(string Stage, string Step, string StepLabel);
public sealed record VdrDisciplineReviewRow(int Id, string Discipline, string Status);
public sealed record VdrSubmissionRow(int Id, int Revision, string? VendorTransmittal, string? ClientTransmittal, DateTime? VendorIssued, string? Purpose, string? Due, string? Ftp, string? CommentSheetNo, DateTime? CommentDate, string? Category, bool Resubmission, bool CanCommentSheet, bool CanReplySheet,
    int? CommentSheetFile, int? ReplySheetFile);
public sealed record VdrFileRow(int Id, string FileName, int Revision, string Kind, string PreviewUrl, string DownloadUrl, bool CanPreview);
public sealed record VdrCommentRow(int Id, string No, int Revision, string? Category, string? Severity, string? Location, string Comment, string? RequiredAction, string? RaisedBy, DateTime Raised, string Status, string? Response, bool CanReply, string? AttachmentUrl, string? ReplyAttachmentUrl);
public sealed record VdrHistoryRow(int Id, DateTime When, string Action, string? From, string? To, string? Decision, string? Comment);
public sealed record VdrLetterRow(int Id, string Type, string Number, string Subject, DateTime Date, string Status);
public sealed record VdrPreviewRow(int Id, string Src, string Title);
public sealed record VdrPinRow(int Id, int Page, double X, double Y, string Note);
public sealed record VdrPinAnswer(int Id);
public sealed record VdrChoice(string Value, string Label);

/// <summary>SCRUM-830: the reads of the VDR item page (VDRController.Details) and the pin of the document viewer as operations over the same services; rights as the controller.</summary>
public interface IVdrDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("VdrDetail", "Head", "VDR.View")]
    Task<List<VdrHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Stages", "VDR.View")]
    Task<List<VdrStageRow>> StagesAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Review", "VDR.View")]
    Task<List<VdrDisciplineReviewRow>> ReviewAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Submissions", "VDR.View")]
    Task<List<VdrSubmissionRow>> SubmissionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Files", "VDR.View")]
    Task<List<VdrFileRow>> FilesAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Comments", "VDR.View")]
    Task<List<VdrCommentRow>> CommentsAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "History", "VDR.View")]
    Task<List<VdrHistoryRow>> HistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Letters", "VDR.View")]
    Task<List<VdrLetterRow>> LettersAsync(int id);

    /// <summary>The main file of the latest submission that has one: what the viewer shows.</summary>
    [PEMS.Application.Common.PemsOperation("VdrDetail", "Preview", "VDR.View")]
    Task<List<VdrPreviewRow>> PreviewAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Pins", "VDR.View")]
    Task<List<VdrPinRow>> PinsAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "SubmissionOptions", "VDR.View")]
    Task<List<VdrChoice>> SubmissionOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("VdrDetail", "Disciplines", "VDR.View")]
    Task<List<VdrChoice>> DisciplinesAsync();

    /// <summary>A pin put on a page of the viewer is a comment of the submission whose main file is shown (the same comment the Add comment form makes, with the note as its text).</summary>
    [PEMS.Application.Common.PemsOperation("VdrDetail", "AddPin", "VDR.Edit")]
    Task<VdrPinAnswer> AddPinAsync(int fileId, int page, double x, double y, string note, string? userName);
}
