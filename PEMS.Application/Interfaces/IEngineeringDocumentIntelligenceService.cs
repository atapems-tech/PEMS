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
    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "ApproveClassification", "EngineeringDocuments.Approve")]   // SCRUM-689
    Task ApproveClassificationAsync(int classificationId, bool applyDiscipline, bool applyVendor,
        bool applyMR, bool applyRevision, bool applyIssuePurpose, int? reviewedById = null);

    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "RejectClassification", "EngineeringDocuments.Create")]   // SCRUM-689
    Task RejectClassificationAsync(int classificationId, int? reviewedById = null);

    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "ApproveTagLink", "EngineeringDocuments.Approve")]   // SCRUM-689
    Task ApproveTagLinkAsync(int tagLinkId, int? reviewedById = null);

    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "RejectTagLink", "EngineeringDocuments.Create")]   // SCRUM-689
    Task RejectTagLinkAsync(int tagLinkId, int? reviewedById = null);

    // پیشرفت دستی در گردش کار: UnderReview -> CommentOpen -> Revision -> Approved -> KnowledgeBase
    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "AdvanceStage", "EngineeringDocuments.Approve")]   // SCRUM-689
    Task AdvanceStageAsync(int documentId, string newStage);

    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "AddComment", "EngineeringDocuments.Create")]   // SCRUM-689
    Task<int> AddCommentAsync(int documentId, string commentText, string? raisedByName);
    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "SetCommentAction", "EngineeringDocuments.Create")]   // SCRUM-689
    Task SetCommentActionStatusAsync(int commentId, string actionStatus);
    [PEMS.Application.Common.PemsOperation("EngineeringIntelligence", "RecordCommentResponse", "EngineeringDocuments.Create")]   // SCRUM-689
    Task RecordCommentResponseAsync(int commentId, string responseText);
}
