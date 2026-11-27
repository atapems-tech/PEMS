namespace PEMS.Application.Interfaces;

public sealed record LetterHeadRow(int Id, string Number, string Subject, string Type, string Status, string Priority, string Confidentiality, string? Project, string? Discipline, string? Sender, string? Receiver, DateTime Date, string? Reference, string? Category,
    bool ResponseRequired, DateTime? ResponseDue, bool ResponseAnswered, bool ResponseOverdue, string? SignedBy, DateTime? SignedDate, string Body, string? Tags,
    bool CanSend, bool CanArchive, bool CanSubmit, bool CanApprove, bool CanIssue, bool CanClose, bool CanReplyRefer, bool CanSign, bool CanResponse, bool CanAudit);
public sealed record LetterRecipientRow(int Id, string Type, string Name, string Read);
public sealed record LetterReferralRow(int Id, string To, string? Instructions, DateTime? Due, string Status, string? ReferredBy, DateTime Referred, string? ResponseNote, bool Overdue, bool CanComplete);
public sealed record LetterAttachmentRow(int Id, string File);
public sealed record LetterStepRow(int Id, int Order, string Approver, string Status, DateTime? Decided, string? Comment, bool Current, bool CanReassign);
public sealed record LetterReviewRow(int Id, string? Reviewer, string Decision, string? Comment, DateTime Created);
public sealed record LetterTimelineRow(int Id, DateTime When, string Type, string Description, string? By);
public sealed record LetterTagRow(int Id, string Name);
public sealed record LetterOption(string Value, string Label);

/// <summary>SCRUM-828: the reads of LetterController.Details as operations: the access rule is the one of the controller (the letter service decides what the viewer may see; a letter the viewer may not see answers nothing), the flags are the ones of the buttons.</summary>
public interface ILetterDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("LetterDetail", "Head", "Correspondence.View")]
    Task<List<LetterHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Recipients", "Correspondence.View")]
    Task<List<LetterRecipientRow>> RecipientsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Referrals", "Correspondence.View")]
    Task<List<LetterReferralRow>> ReferralsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Attachments", "Correspondence.View")]
    Task<List<LetterAttachmentRow>> AttachmentsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Steps", "Correspondence.View")]
    Task<List<LetterStepRow>> StepsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Reviews", "Correspondence.View")]
    Task<List<LetterReviewRow>> ReviewsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Timeline", "Correspondence.View")]
    Task<List<LetterTimelineRow>> TimelineAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Tags", "Correspondence.View")]
    Task<List<LetterTagRow>> TagsAsync(int id);

    [PEMS.Application.Common.PemsOperation("LetterDetail", "TagOptions", "Correspondence.View")]
    Task<List<LetterOption>> TagOptionsAsync();

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Departments", "Correspondence.View")]
    Task<List<LetterOption>> DepartmentsAsync();

    [PEMS.Application.Common.PemsOperation("LetterDetail", "Approvers", "Correspondence.View")]
    Task<List<LetterOption>> ApproversAsync();
}
