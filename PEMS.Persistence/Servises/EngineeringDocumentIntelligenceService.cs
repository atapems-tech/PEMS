using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی جریان Document Intelligence: از پیشنهادهای AI (Draft) تا تایید انسانی و نوشتن
/// نهایی روی EngineeringDocument - دقیقاً همان الگوی «بازبینی قبل از ذخیره‌ی نهایی» که در VDR
/// و AI Extraction استفاده شد.
/// </summary>
public class EngineeringDocumentIntelligenceService : IEngineeringDocumentIntelligenceService
{
    private readonly PEMSDbContext _context;
    private readonly IVendorService _vendorService;

    private static readonly string[] StageOrder =
    {
        "Uploaded", "AIProcessing", "Classified", "DisciplineAssigned",
        "UnderReview", "CommentOpen", "Revision", "Approved", "KnowledgeBase"
    };

    public EngineeringDocumentIntelligenceService(PEMSDbContext context, IVendorService vendorService)
    {
        _context = context;
        _vendorService = vendorService;
    }

    public async Task<DocumentIntelligenceDashboardDto> GetDashboardAsync()
    {
        var documents = await _context.EngineeringDocuments.Where(x => x.IsActive).ToListAsync();

        return new DocumentIntelligenceDashboardDto
        {
            TotalDocuments = documents.Count,
            ByStage = documents.GroupBy(x => x.AIWorkflowStage).ToDictionary(g => g.Key, g => g.Count()),
            PendingClassificationReview = await _context.AIDocumentClassifications.CountAsync(x => x.ReviewStatus == "Draft"),
            PendingTagLinkReview = await _context.DocumentTagLinks.CountAsync(x => x.IsAIProposed && x.ReviewStatus == "Draft"),
            OpenComments = await _context.DocumentComments.CountAsync(x => x.ActionStatus != "Closed" && x.ActionStatus != "NoActionRequired")
        };
    }

    public async Task<List<DocumentIntelligenceDetailDto>> GetPendingReviewAsync()
    {
        var documentIds = await _context.AIDocumentClassifications
            .Where(x => x.ReviewStatus == "Draft" && x.DocumentId.HasValue)
            .Select(x => x.DocumentId!.Value)
            .Distinct()
            .ToListAsync();

        var result = new List<DocumentIntelligenceDetailDto>();

        foreach (var id in documentIds)
        {
            var detail = await GetDetailAsync(id);
            if (detail != null) result.Add(detail);
        }

        return result;
    }

    public async Task<DocumentIntelligenceDetailDto?> GetDetailAsync(int documentId)
    {
        var document = await _context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document == null) return null;

        var classifications = await _context.AIDocumentClassifications
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var tagLinks = await _context.DocumentTagLinks
            .Include(x => x.EquipmentTag)
            .Where(x => x.DocumentId == documentId)
            .ToListAsync();

        var comments = await _context.DocumentComments
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.RaisedDate)
            .ToListAsync();

        var aiProcessingStatus = await _context.DocumentAttachments
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.AIProcessedDate)
            .Select(x => x.AIStatus)
            .FirstOrDefaultAsync();

        return new DocumentIntelligenceDetailDto
        {
            DocumentId = document.DocumentId,
            DocumentNo = document.DocumentNo,
            Title = document.Title,
            AIWorkflowStage = document.AIWorkflowStage,
            Status = document.Status,
            AIProcessingStatus = aiProcessingStatus,

            Classifications = classifications.Select(c => new AIDocumentClassificationDto
            {
                AIDocumentClassificationId = c.AIDocumentClassificationId,
                DocumentId = (int)c.DocumentId,
                AttachmentId = c.AttachmentId,
                PredictedDocumentType = c.PredictedDocumentType,
                PredictedDisciplineCode = c.PredictedDisciplineCode,
                PredictedRevisionNo = c.PredictedRevisionNo,
                PredictedIssuePurpose = c.PredictedIssuePurpose,
                PredictedVendorName = c.PredictedVendorName,
                PredictedMRNumber = c.PredictedMRNumber,
                ConfidenceScore = c.ConfidenceScore,
                ReviewStatus = c.ReviewStatus
            }).ToList(),

            TagLinks = tagLinks.Select(t => new EngineeringDocumentTagLinkDto
            {
                EngineeringDocumentTagLinkId = t.DocumentTagLinkId,
                EquipmentTagId = t.EquipmentTagId,
                TagNumber = t.EquipmentTag?.TagNumber ?? "",
                IsAIProposed = t.IsAIProposed,
                ReviewStatus = t.ReviewStatus,
                Confidence = t.Confidence
            }).ToList(),

            Comments = comments.Select(c => new EngineeringDocumentCommentDto
            {
                EngineeringDocumentCommentId = c.DocumentCommentId,
                CommentText = c.CommentText,
                RaisedByName = c.RaisedByName,
                RaisedDate = c.RaisedDate,
                ActionStatus = c.ActionStatus,
                ResponseText = c.ResponseText,
                ResponseDate = c.ResponseDate
            }).ToList()
        };
    }

    public async Task ApproveClassificationAsync(int classificationId, bool applyDiscipline, bool applyVendor,
        bool applyMR, bool applyRevision, bool applyIssuePurpose, int? reviewedById = null)
    {
        var classification = await _context.AIDocumentClassifications
            .FirstOrDefaultAsync(x => x.AIDocumentClassificationId == classificationId);

        if (classification == null) throw new Exception("رکورد Classification یافت نشد");

        var document = await _context.EngineeringDocuments
            .FirstOrDefaultAsync(x => x.DocumentId == classification.DocumentId);

        if (document == null) throw new Exception("مدرک مرتبط یافت نشد");

        if (applyDiscipline && !string.IsNullOrWhiteSpace(classification.PredictedDisciplineCode))
        {
            var discipline = await _context.Disciplines
                .FirstOrDefaultAsync(x => x.Code.ToLower() == classification.PredictedDisciplineCode!.ToLower()
                    || x.Name.ToLower() == classification.PredictedDisciplineCode!.ToLower());

            if (discipline != null)
                document.DisciplineId = discipline.DisciplineId;
        }

        if (applyVendor && !string.IsNullOrWhiteSpace(classification.PredictedVendorName))
        {
            document.VendorId = await _vendorService.FindOrCreateByNameAsync(classification.PredictedVendorName!);
        }

        if (applyMR && !string.IsNullOrWhiteSpace(classification.PredictedMRNumber))
        {
            var mr = await _context.MaterialRequisitions
                .FirstOrDefaultAsync(x => x.MRNumber == classification.PredictedMRNumber);

            if (mr != null)
                document.RelatedMRId = mr.MRId;
        }

        if (applyRevision && !string.IsNullOrWhiteSpace(classification.PredictedRevisionNo))
            document.RevisionNo = classification.PredictedRevisionNo!;

        if (applyIssuePurpose && !string.IsNullOrWhiteSpace(classification.PredictedIssuePurpose))
            document.IssuePurpose = classification.PredictedIssuePurpose;

        if (!string.IsNullOrWhiteSpace(classification.PredictedDocumentType))
            document.DocumentType = classification.PredictedDocumentType;

        classification.ReviewStatus = "Approved";
        classification.ReviewedById = reviewedById;
        classification.ReviewedDate = DateTime.Now;

        document.AIWorkflowStage = "DisciplineAssigned";
        document.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task RejectClassificationAsync(int classificationId, int? reviewedById = null)
    {
        var classification = await _context.AIDocumentClassifications
            .FirstOrDefaultAsync(x => x.AIDocumentClassificationId == classificationId);

        if (classification == null) return;

        classification.ReviewStatus = "Rejected";
        classification.ReviewedById = reviewedById;
        classification.ReviewedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task ApproveTagLinkAsync(int tagLinkId, int? reviewedById = null)
    {
        var link = await _context.DocumentTagLinks.FirstOrDefaultAsync(x => x.DocumentTagLinkId == tagLinkId);
        if (link == null) return;

        link.ReviewStatus = "Approved";
        await _context.SaveChangesAsync();
    }

    public async Task RejectTagLinkAsync(int tagLinkId, int? reviewedById = null)
    {
        var link = await _context.DocumentTagLinks.FirstOrDefaultAsync(x => x.DocumentTagLinkId == tagLinkId);
        if (link == null) return;

        link.ReviewStatus = "Rejected";
        await _context.SaveChangesAsync();
    }

    public async Task AdvanceStageAsync(int documentId, string newStage)
    {
        if (!StageOrder.Contains(newStage))
            throw new Exception($"مرحله‌ی نامعتبر: {newStage}");

        var document = await _context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document == null) throw new Exception("مدرک یافت نشد");

        document.AIWorkflowStage = newStage;
        document.ModifiedDate = DateTime.Now;

        if (newStage == "Approved")
            document.Status = "Approved";

        await _context.SaveChangesAsync();
    }

    public async Task<int> AddCommentAsync(int documentId, string commentText, string? raisedByName)
    {
        var comment = new DocumentComment
        {
            DocumentId = documentId,
            CommentText = commentText,
            RaisedByName = raisedByName,
            RaisedDate = DateTime.Now,
            ActionStatus = "OpenComment"
        };

        await _context.DocumentComments.AddAsync(comment);

        var document = await _context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document != null && document.AIWorkflowStage != "CommentOpen")
            document.AIWorkflowStage = "CommentOpen";

        await _context.SaveChangesAsync();

        return comment.DocumentCommentId;
    }

    public async Task SetCommentActionStatusAsync(int commentId, string actionStatus)
    {
        var comment = await _context.DocumentComments.FirstOrDefaultAsync(x => x.DocumentCommentId == commentId);
        if (comment == null) return;

        comment.ActionStatus = actionStatus;

        if (actionStatus == "Closed")
            comment.ClosedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task RecordCommentResponseAsync(int commentId, string responseText)
    {
        var comment = await _context.DocumentComments.FirstOrDefaultAsync(x => x.DocumentCommentId == commentId);
        if (comment == null) return;

        comment.ResponseText = responseText;
        comment.ResponseDate = DateTime.Now;

        if (comment.ActionStatus is "OpenComment" or "ResponsePending")
            comment.ActionStatus = "Verified";

        await _context.SaveChangesAsync();
    }
}
