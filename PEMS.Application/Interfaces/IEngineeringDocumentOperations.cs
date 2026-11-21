namespace PEMS.Application.Interfaces;

/// <summary>
/// SCRUM-690: the lifecycle commands of the engineering document details page as operations (freeze, hold, the review and approval steps, the issue, the client response, the comment action and the relationship
/// analysis). Each keeps the status rule and the messages of EngineeringDocumentsController; the module permission is the floor of the operation, and the separation-of-duties guard of the approval is the
/// <c>guard</c> of the action definition. The actor (employee, discipline, roles, address, browser) comes from the context of the signed-in user. The list, the details page, create, edit, delete, the
/// attachments and the new revision stay on the controller until the form engine can express them.
/// </summary>
public interface IEngineeringDocumentOperations
{
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Freeze", "EngineeringDocuments.Freeze")]
    Task<(bool Success, string? Error)> FreezeAsync(int id, string? reason, int? actorEmployeeId);

    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Unfreeze", "EngineeringDocuments.Unfreeze")]
    Task<(bool Success, string? Error)> UnfreezeAsync(int id, string? reason, int? actorEmployeeId);

    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Hold", "EngineeringDocuments.Hold")]
    Task<(bool Success, string? Error)> HoldAsync(int id, string reason, int? responsibleDisciplineId, int? actorEmployeeId);

    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "ReleaseHold", "EngineeringDocuments.ReleaseHold")]
    Task<(bool Success, string? Error)> ReleaseHoldAsync(int id, string reason, int? actorEmployeeId);

    /// <summary>Only a Draft or a Rejected document goes to review.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Submit")]
    Task<(bool Success, string? Error)> SubmitAsync(int id, string? comment, List<int>? idcDisciplineIds, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    /// <summary>Only a Submitted document is checked.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Check")]
    Task<(bool Success, string? Error)> CheckAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    /// <summary>The answer of one discipline to the inter-discipline check (Approve, Comment or Reject); the user needs an employee with a discipline.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "IdcRespond")]
    Task<(bool Success, string? Error)> IdcRespondAsync(int id, string decision, string? comment, int? actorEmployeeId, int? actorDisciplineId, string? ipAddress, string? userAgent);

    /// <summary>Only a Checked document is approved, and only by a user of its own discipline (unless the role may approve every discipline); the separation of duties is the guard of the action.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Approve", "EngineeringDocuments.Approve")]
    Task<(bool Success, string? Error)> ApproveAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    /// <summary>A Submitted or a Checked document is rejected.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Reject")]
    Task<(bool Success, string? Error)> RejectAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    /// <summary>Only an Approved document is issued.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "Issue")]
    Task<(bool Success, string? Error)> IssueAsync(int id, string? comment, string deliveryUnitType, int? deliveryDepartmentId, string? externalReceiverName, string? letterNumber, string? letterSubject, string? letterBody,
        string? issuePurpose, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    /// <summary>A Submitted or a Checked document goes back to its author for correction.</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "ReviseAndResubmit")]
    Task<(bool Success, string? Error)> ReviseAndResubmitAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "RecordClientResponse")]
    Task<(bool Success, string? Error)> RecordClientResponseAsync(int id, string clientStatus, int incomingLetterId, string? clientComment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent);

    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "SetCommentAction")]
    Task<(bool Success, string? Error)> SetCommentActionAsync(int commentId, string actionStatus);

    /// <summary>Asks the AI copilot for semantic relationships of the document (an offline model: none found is not an error).</summary>
    [PEMS.Application.Common.PemsOperation("EngineeringDocument", "AnalyzeRelationships")]
    Task<(bool Success, string? Error)> AnalyzeRelationshipsAsync(int id);
}
