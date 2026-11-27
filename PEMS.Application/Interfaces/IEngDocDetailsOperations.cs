namespace PEMS.Application.Interfaces;

public sealed record EngDocHeadRow(int Id, string DocumentNo, string Title, string Status, string Revision, bool Frozen, bool OnHold, string? Project, string? Discipline, string? Area, string? Type, DateTime Created, string? Description, string? IssuePurpose, string? PreparedBy, string? CheckedBy, string? ApprovedBy,
    string? CommentStatus, string? ClientResponse, string? Equipment, int? EquipmentId, string? Contract, int? ContractId, string? NextStep, string? FrozenNote, string? PidOf, string Stage, string StageLabel,
    bool CanSubmit, bool CanCheck, bool CanReject, bool CanRevise, bool CanIdc, string? Waiting, bool CanApprove, bool CanIssue, bool CanClient, bool CanNewRevision, bool CanFreeze, bool CanUnfreeze, bool CanHold, bool CanRelease, bool CanAssign, string SubmitLabel);
public sealed record EngDocStageRow(string Stage, string Step, string StepLabel);
public sealed record EngDocFileRow(int Id, string FileName, string? Revision, string? Role, string Official, string Size, string? AiStatus, DateTime Uploaded, bool AiDone, string Verified);
public sealed record EngDocIdcRow(int Id, string Discipline, string Status, string? Comment, string? RespondedBy, DateTime? Responded);
public sealed record EngDocRevisionRow(int Id, string Revision, string Status, string? Reason, string? CreatedBy, string? Reviewer, string? ApprovedBy, DateTime Created, DateTime? Issued, DateTime? Effective, string? Supersedes, string? Change, string? VendorComment, string? ClientReview);
public sealed record EngDocHistoryRow(int Id, DateTime When, string Action, string? From, string? To, string? Decision, string? By, string? Comment);
public sealed record EngDocHoldRow(int Id, string Reason, DateTime Held, string? By, string? Discipline, DateTime? Released, string? ReleasedBy, string? ReleaseReason);
public sealed record EngDocTransmittalRow(int Id, string No, string Status, DateTime Date, DateTime? Sent, string Purpose, string Revision);
public sealed record EngDocLetterRow(int Id, string Type, string Number, string Subject, DateTime Date, string Status);
public sealed record EngDocCommentRow(int Id, string No, string? Category, string? Severity, string? Location, string Comment, string? RequiredAction, string? RaisedBy, DateTime Raised, string Status, string? Response, bool CanReply, bool MarksRevision, string? AttachmentUrl, string? ReplyAttachmentUrl);
public sealed record EngDocPreviewRow(int Id, string Src, string Title);
public sealed record EngDocPinRow(int Id, int Page, double X, double Y, string Note);
public sealed record EngDocPinAnswer(int Id);
public sealed record EngDocOption(string Value, string Label);

/// <summary>SCRUM-838: the reads of the engineering document page (EngineeringDocumentsController.Details), with the discipline scope the controller applied, and the pin of the viewer, as operations.</summary>
public interface IEngDocDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Head", "EngineeringDocuments.View")]
    Task<List<EngDocHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Stages", "EngineeringDocuments.View")]
    Task<List<EngDocStageRow>> StagesAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Files", "EngineeringDocuments.View")]
    Task<List<EngDocFileRow>> FilesAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Idc", "EngineeringDocuments.View")]
    Task<List<EngDocIdcRow>> IdcAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Revisions", "EngineeringDocuments.View")]
    Task<List<EngDocRevisionRow>> RevisionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "History", "EngineeringDocuments.View")]
    Task<List<EngDocHistoryRow>> HistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Holds", "EngineeringDocuments.View")]
    Task<List<EngDocHoldRow>> HoldsAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Transmittals", "EngineeringDocuments.View")]
    Task<List<EngDocTransmittalRow>> TransmittalsAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Letters", "EngineeringDocuments.View")]
    Task<List<EngDocLetterRow>> LettersAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Comments", "EngineeringDocuments.View")]
    Task<List<EngDocCommentRow>> CommentsAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Preview", "EngineeringDocuments.View")]
    Task<List<EngDocPreviewRow>> PreviewAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Pins", "EngineeringDocuments.View")]
    Task<List<EngDocPinRow>> PinsAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "SubmitDisciplines", "EngineeringDocuments.View")]
    Task<List<EngDocOption>> SubmitDisciplinesAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "AllDisciplines", "EngineeringDocuments.View")]
    Task<List<EngDocOption>> AllDisciplinesAsync();

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "Departments", "EngineeringDocuments.View")]
    Task<List<EngDocOption>> DepartmentsAsync();

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "IncomingLetters", "EngineeringDocuments.View")]
    Task<List<EngDocOption>> IncomingLettersAsync(int id);

    [PEMS.Application.Common.PemsOperation("EngDocDetail", "OpenComments", "EngineeringDocuments.View")]
    Task<List<EngDocOption>> OpenCommentsAsync(int id);

    /// <summary>A pin on a page of the viewer is a comment of the document (the note as its text).</summary>
    [PEMS.Application.Common.PemsOperation("EngDocDetail", "AddPin", "EngineeringDocuments.Edit")]
    Task<EngDocPinAnswer> AddPinAsync(int fileId, int page, double x, double y, string note, string? userName);
}
