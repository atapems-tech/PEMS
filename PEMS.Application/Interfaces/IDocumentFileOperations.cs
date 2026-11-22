using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-690, 761, 685: the commands of the engineering document and VDR pages that carry a file (a comment or reply with an attachment, a comment or reply sheet). Same storage, same rows, same texts as the controllers had; the upload profile of the action checks the file.</summary>
public interface IDocumentFileOperations
{
    [PemsOperation("EngineeringDocumentFiles", "AddComment")]
    Task<(bool Success, string? Error)> AddEngineeringCommentAsync(int documentId, string commentText, string? raisedByName, string? sheet, string? zone, string? reference, string? category, string? severity, string? requiredAction,
        DateTime? dueDate, OperationFile? attachment, int? pageNumber, double? pinX, double? pinY, bool marksRevisionRequired);

    [PemsOperation("EngineeringDocumentFiles", "RecordCommentResponse")]
    Task<(bool Success, string? Error)> RecordEngineeringResponseAsync(int commentId, string? responseText, OperationFile? replyAttachment);

    [PemsOperation("VdrFiles", "AddComment")]
    Task<(bool Success, string? Error)> AddVdrCommentAsync(int vdrItemId, int vdrSubmissionId, string commentText, string? raisedByName, string? sheet, string? zone, string? reference, string? category, string? severity, string? requiredAction,
        DateTime? dueDate, OperationFile? attachment, int? pageNumber, double? pinX, double? pinY);

    [PemsOperation("VdrFiles", "RecordCommentResponse")]
    Task<(bool Success, string? Error)> RecordVdrResponseAsync(int commentId, string? responseText, OperationFile? replyAttachment);

    /// <summary>The file of the Files tab: stored in PEMS and linked to the submission as its document, comment sheet or reply sheet (kind Main, CommentSheet, ReplySheet); a new file replaces the link of that kind.</summary>
    [PemsOperation("VdrFiles", "UploadSubmissionFile", "VDR.Edit")]
    Task<(bool Success, string? Error)> UploadVdrSubmissionFileAsync(int vdrItemId, int submissionId, string kind, OperationFile? file);

    [PemsOperation("VdrFiles", "UploadCommentSheet")]
    Task<(bool Success, string? Error)> UploadVdrCommentSheetAsync(int submissionId, OperationFile file);

    [PemsOperation("VdrFiles", "UploadReplySheet")]
    Task<(bool Success, string? Error)> UploadVdrReplySheetAsync(int submissionId, OperationFile file);
}
