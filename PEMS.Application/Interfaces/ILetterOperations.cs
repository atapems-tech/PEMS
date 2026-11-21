namespace PEMS.Application.Interfaces;

/// <summary>
/// SCRUM-716, 715: the commands of the letter details page as operations: the letter's own access check (a letter the user may not see is refused), the response requirement, the star, the recipients, the tags,
/// the review steps, the issue and close, the draft edits, the referrals, the signature, and the send, archive, approver change and draft discard of the compose workspace. Each keeps the rule and the texts of
/// LetterController and LetterComposeController. The list, the details page, create, reply, the composer's own JSON endpoints, the bulk bar and the files stay on the controllers.
/// </summary>
public interface ILetterOperations
{
    [PEMS.Application.Common.PemsOperation("Letter", "SetResponseDue")]
    Task<(bool Success, string? Error)> SetResponseDueAsync(int letterId, bool responseRequired, DateTime? responseDueDate);

    [PEMS.Application.Common.PemsOperation("Letter", "ToggleStar")]
    Task<(bool Success, string? Error)> ToggleStarAsync(int letterId);

    [PEMS.Application.Common.PemsOperation("Letter", "AddRecipient")]
    Task<(bool Success, string? Error)> AddRecipientAsync(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName);

    [PEMS.Application.Common.PemsOperation("Letter", "MarkRecipientRead")]
    Task<(bool Success, string? Error)> MarkRecipientReadAsync(int letterId, int letterRecipientId);

    /// <summary>An existing tag, or a new one named here (with its colour).</summary>
    [PEMS.Application.Common.PemsOperation("Letter", "AttachTag")]
    Task<(bool Success, string? Error)> AttachTagAsync(int letterId, int? tagId, string? newTagName, string? newTagColor);

    [PEMS.Application.Common.PemsOperation("Letter", "DetachTag")]
    Task<(bool Success, string? Error)> DetachTagAsync(int letterId, int tagId);

    [PEMS.Application.Common.PemsOperation("Letter", "SubmitForReview")]
    Task<(bool Success, string? Error)> SubmitForReviewAsync(int letterId);

    /// <summary>The reviewer of the current step (or a user who sees every letter) approves.</summary>
    [PEMS.Application.Common.PemsOperation("Letter", "Approve")]
    Task<(bool Success, string? Error)> ApproveAsync(int letterId, string? comment);

    [PEMS.Application.Common.PemsOperation("Letter", "ReturnForRevision")]
    Task<(bool Success, string? Error)> ReturnForRevisionAsync(int letterId, string reason);

    [PEMS.Application.Common.PemsOperation("Letter", "Reject")]
    Task<(bool Success, string? Error)> RejectAsync(int letterId, string reason);

    [PEMS.Application.Common.PemsOperation("Letter", "Issue", "Correspondence.Approve")]
    Task<(bool Success, string? Error)> IssueAsync(int letterId);

    [PEMS.Application.Common.PemsOperation("Letter", "Close", "Correspondence.Approve")]
    Task<(bool Success, string? Error)> CloseAsync(int letterId);

    /// <summary>rowVersion is the base64 text of the version the form was loaded with (a concurrent edit is refused).</summary>
    [PEMS.Application.Common.PemsOperation("Letter", "SaveDraft")]
    Task<(bool Success, string? Error)> SaveDraftAsync(int letterId, string subject, string body, string? rowVersion);

    [PEMS.Application.Common.PemsOperation("Letter", "Resubmit")]
    Task<(bool Success, string? Error)> ResubmitAsync(int letterId, string subject, string body, string? rowVersion);

    [PEMS.Application.Common.PemsOperation("Letter", "Refer")]
    Task<(bool Success, string? Error)> ReferAsync(int letterId, int? referredToEmployeeId, int? referredToDepartmentId, string? instructions, DateTime? dueDate);

    [PEMS.Application.Common.PemsOperation("Letter", "MarkReferralRead")]
    Task<(bool Success, string? Error)> MarkReferralReadAsync(int referralId, int letterId);

    [PEMS.Application.Common.PemsOperation("Letter", "CompleteReferral")]
    Task<(bool Success, string? Error)> CompleteReferralAsync(int referralId, int letterId, string? responseNote);

    [PEMS.Application.Common.PemsOperation("Letter", "Sign")]
    Task<(bool Success, string? Error)> SignAsync(int letterId, string signedByName);

    [PEMS.Application.Common.PemsOperation("Letter", "Send")]
    Task<(bool Success, string? Error)> SendAsync(int letterId);

    [PEMS.Application.Common.PemsOperation("Letter", "Archive")]
    Task<(bool Success, string? Error)> ArchiveAsync(int letterId);

    [PEMS.Application.Common.PemsOperation("Letter", "ReassignApprover")]
    Task<(bool Success, string? Error)> ReassignApproverAsync(int letterId, int stepOrder, int newApproverUserId);

    /// <summary>Discards a draft the user composed (the compose workspace).</summary>
    [PEMS.Application.Common.PemsOperation("Letter", "DiscardDraft")]
    Task<(bool Success, string? Error)> DiscardDraftAsync(int letterId);
}
