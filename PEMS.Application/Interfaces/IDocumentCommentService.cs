using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Comment Management طبق استاندارد Comment Sheet/Reply Sheet صنعت EPC - قبلاً IVDRCommentService
/// نام داشت، ولی از اول روی جدول مشترک DocumentComment کار می‌کرد (هم مدارک مهندسی، هم VDR).
/// طبق درخواست: مدارک مهندسی هم باید دقیقاً همان چرخه‌ی کامنت/ریپلای VDR را داشته باشند - این
/// سرویس حالا مستقیم با DocumentId کار می‌کند (نه فقط از طریق VDRSubmissionId).
/// </summary>
public interface IDocumentCommentService
{
    Task<List<VDRCommentDto>> GetForDocumentAsync(int documentId);

    Task<int> AddCommentAsync(int documentId, string commentText, string? raisedByName,
        int? vdrSubmissionId = null, string? sheet = null, string? zone = null, string? reference = null,
        string? category = null, string? severity = null, string? requiredAction = null, DateTime? dueDate = null,
        int? attachmentId = null, int? pageNumber = null, double? pinX = null, double? pinY = null,
        bool marksRevisionRequired = false);

    Task SetActionStatusAsync(int commentId, string actionStatus);

    Task RecordResponseAsync(int commentId, string? responseText, int? replyAttachmentId = null);
}
