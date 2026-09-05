using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// جریان کامل Document Intelligence طبق سند راهنما:
/// Upload → AI Processing → Classification → Discipline Assignment → Engineering Review →
/// Comment → Revision → Approval → Knowledge Base
/// </summary>
public interface IEngineeringDocumentIntelligenceService
{
    Task<DocumentIntelligenceDashboardDto> GetDashboardAsync();

    Task<List<DocumentIntelligenceDetailDto>> GetPendingReviewAsync();

    Task<DocumentIntelligenceDetailDto?> GetDetailAsync(int documentId);

    // تایید پیشنهاد AI برای Classification: فیلدهای انتخاب‌شده روی خود EngineeringDocument نوشته
    // می‌شوند (Discipline/Vendor/MR/RevisionNo/IssuePurpose) و مرحله به DisciplineAssigned می‌رود
    Task ApproveClassificationAsync(int classificationId, bool applyDiscipline, bool applyVendor,
        bool applyMR, bool applyRevision, bool applyIssuePurpose, int? reviewedById = null);

    Task RejectClassificationAsync(int classificationId, int? reviewedById = null);

    Task ApproveTagLinkAsync(int tagLinkId, int? reviewedById = null);

    Task RejectTagLinkAsync(int tagLinkId, int? reviewedById = null);

    // پیشرفت دستی در گردش کار: UnderReview -> CommentOpen -> Revision -> Approved -> KnowledgeBase
    Task AdvanceStageAsync(int documentId, string newStage);

    Task<int> AddCommentAsync(int documentId, string commentText, string? raisedByName);
    Task SetCommentActionStatusAsync(int commentId, string actionStatus);
    Task RecordCommentResponseAsync(int commentId, string responseText);
}
