using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.AI.Interfaces;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class DocumentAIIngestionService : IDocumentAIIngestionService
{
    private readonly PEMSDbContext _context;
    private readonly IDocumentAIService _documentAIService;
    private readonly ILogger<DocumentAIIngestionService> _logger;

    public DocumentAIIngestionService(
        PEMSDbContext context,
        IDocumentAIService documentAIService,
        ILogger<DocumentAIIngestionService> logger)
    {
        _context = context;
        _documentAIService = documentAIService;
        _logger = logger;
    }

    public async Task ProcessAttachmentAsync(int attachmentId)
    {
        var attachment = await _context.DocumentAttachments
            .FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);

        if (attachment == null)
        {
            _logger.LogWarning("پیوست با شناسه {AttachmentId} برای پردازش هوش مصنوعی یافت نشد", attachmentId);
            return;
        }

        attachment.AIStatus = "Processing";

        var parentDocument = await _context.EngineeringDocuments
            .FirstOrDefaultAsync(x => x.DocumentId == attachment.DocumentId);

        if (parentDocument != null)
            parentDocument.AIWorkflowStage = "AIProcessing";

        await _context.SaveChangesAsync();

        var physicalPath = ResolvePhysicalPath(attachment.FilePath);

        if (!File.Exists(physicalPath))
        {
            attachment.AIStatus = "Failed";
            attachment.AIError = "فایل روی دیسک یافت نشد.";
            attachment.AIProcessedDate = DateTime.Now;
            // طبق PEMS_ENGINEERING_DOMAIN_BLUEPRINT_V2.md بخش ۴: اگه AI شکست بخوره، سند نباید
            // برای همیشه روی "AIProcessing" گیر کنه - وضعیت شکست فقط روی Attachment دیده می‌شد
            if (parentDocument != null) parentDocument.AIWorkflowStage = "Uploaded";
            await _context.SaveChangesAsync();
            return;
        }

        var result = await _documentAIService.AnalyzeDocumentAsync(physicalPath, attachment.FileExtension);

        attachment.AIProcessedDate = DateTime.Now;

        if (!result.Success)
        {
            attachment.AIStatus = "Failed";
            attachment.AIError = result.ErrorMessage;
            if (parentDocument != null) parentDocument.AIWorkflowStage = "Uploaded";
            await _context.SaveChangesAsync();
            return;
        }

        attachment.AIStatus = "Completed";
        attachment.AIError = null;
        attachment.AISummary = result.Summary;
        attachment.AIKeywords = result.Keywords.Count > 0 ? string.Join(", ", result.Keywords) : null;
        attachment.AITags = result.ExtractedTags.Count > 0 ? string.Join(", ", result.ExtractedTags) : null;
        attachment.AIConfidence = result.Confidence;

        if (result.ExtractedData != null)
        {
            var oldPendingTags = await _context.AIExtractedTags
                .Where(x => x.AttachmentId == attachmentId && x.ReviewStatus == "Draft")
                .ToListAsync();
            _context.AIExtractedTags.RemoveRange(oldPendingTags);

            var oldPendingSpecs = await _context.AIExtractedSpecifications
                .Where(x => x.AttachmentId == attachmentId && x.ReviewStatus == "Draft")
                .ToListAsync();
            _context.AIExtractedSpecifications.RemoveRange(oldPendingSpecs);

            foreach (var tag in result.ExtractedData.EquipmentTags)
            {
                _context.AIExtractedTags.Add(new AIExtractedTag
                {
                    AttachmentId = attachmentId,
                    TagNumber = tag.TagNumber,
                    TagType = tag.TagType,
                    Area = tag.Area,
                    Service = tag.Service,
                    Description = tag.Description,
                    Confidence = result.Confidence,
                    ReviewStatus = "Draft",
                    CreatedDate = DateTime.Now
                });
            }

            foreach (var spec in result.ExtractedData.Specifications)
            {
                _context.AIExtractedSpecifications.Add(new AIExtractedSpecification
                {
                    AttachmentId = attachmentId,
                    RelatedTagNumber = spec.RelatedTagNumber,
                    FieldName = spec.FieldName,
                    FieldValue = spec.FieldValue,
                    Unit = spec.Unit,
                    Confidence = result.Confidence,
                    ReviewStatus = "Draft",
                    CreatedDate = DateTime.Now
                });
            }


            // =========================
            // Document-level Classification (AVEVA-style: خلاصه‌ی پیشنهادی برای تایید انسانی)
            // =========================

            if (parentDocument != null)
            {
                var oldClassifications = await _context.AIDocumentClassifications
                    .Where(x => x.AttachmentId == attachmentId && x.ReviewStatus == "Draft")
                    .ToListAsync();
                _context.AIDocumentClassifications.RemoveRange(oldClassifications);

                await _context.AIDocumentClassifications.AddAsync(new AIDocumentClassification
                {
                    DocumentId = parentDocument.DocumentId,
                    AttachmentId = attachmentId,
                    PredictedDocumentType = result.ExtractedData.DocumentInfo?.DocumentType,
                    PredictedDisciplineCode = result.ExtractedData.DocumentInfo?.Discipline,
                    PredictedRevisionNo = result.ExtractedData.DocumentInfo?.RevisionNo,
                    PredictedIssuePurpose = result.ExtractedData.DocumentInfo?.IssuePurpose,
                    PredictedVendorName = result.ExtractedData.DocumentInfo?.VendorName,
                    PredictedMRNumber = result.ExtractedData.DocumentInfo?.RelatedMRNumber,
                    ConfidenceScore = result.Confidence,
                    ReviewStatus = "Draft",
                    CreatedDate = DateTime.Now
                });

                // پیشنهاد ارتباط با Tag های موجود (فقط اگر Tag از قبل در سیستم ثبت شده باشد؛
                // Tag های تازه از طریق همان جریان تایید AIExtractedTag ساخته می‌شوند)
                var oldTagLinkProposals = await _context.DocumentTagLinks
                    .Where(x => x.DocumentId == parentDocument.DocumentId && x.IsAIProposed && x.ReviewStatus == "Draft")
                    .ToListAsync();
                _context.DocumentTagLinks.RemoveRange(oldTagLinkProposals);

                foreach (var tag in result.ExtractedData.EquipmentTags)
                {
                    var existingTag = await _context.EquipmentTags
                        .FirstOrDefaultAsync(x => x.TagNumber == tag.TagNumber && x.IsActive);

                    if (existingTag == null)
                        continue;

                    await _context.DocumentTagLinks.AddAsync(new DocumentTagLink
                    {
                        DocumentId = parentDocument.DocumentId,
                        EquipmentTagId = existingTag.TagId,
                        IsAIProposed = true,
                        ReviewStatus = "Draft",
                        Confidence = result.Confidence,
                        CreatedDate = DateTime.Now
                    });
                }

                parentDocument.AIWorkflowStage = "Classified";
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task ApproveExtractedTagAsync(int extractedTagId, int equipmentId, int? reviewedById = null)
    {
        var extractedTag = await _context.AIExtractedTags.FirstOrDefaultAsync(x => x.ExtractedTagId == extractedTagId);
        if (extractedTag == null) throw new Exception("رکورد استخراج‌شده یافت نشد");

        var equipmentExists = await _context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId);
        if (!equipmentExists) throw new Exception("تجهیز انتخاب‌شده یافت نشد");

        var existingTag = await _context.EquipmentTags
            .FirstOrDefaultAsync(x => x.TagNumber == extractedTag.TagNumber && x.IsActive);

        EquipmentTag equipmentTag;
        if (existingTag != null)
        {
            equipmentTag = existingTag;
        }
        else
        {
            equipmentTag = new EquipmentTag
            {
                EquipmentId = equipmentId,
                TagNumber = extractedTag.TagNumber,
                TagType = extractedTag.TagType ?? string.Empty,
                Area = extractedTag.Area,
                Service = extractedTag.Service,
                Description = extractedTag.Description,
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            _context.EquipmentTags.Add(equipmentTag);
            await _context.SaveChangesAsync();
        }

        extractedTag.ReviewStatus = "Approved";
        extractedTag.LinkedEquipmentId = equipmentId;
        extractedTag.CreatedTagId = equipmentTag.TagId;
        extractedTag.ReviewedById = reviewedById;
        extractedTag.ReviewedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task RejectExtractedTagAsync(int extractedTagId, string? comment = null, int? reviewedById = null)
    {
        var extractedTag = await _context.AIExtractedTags.FirstOrDefaultAsync(x => x.ExtractedTagId == extractedTagId);
        if (extractedTag == null) throw new Exception("رکورد استخراج‌شده یافت نشد");

        extractedTag.ReviewStatus = "Rejected";
        extractedTag.ReviewComment = comment;
        extractedTag.ReviewedById = reviewedById;
        extractedTag.ReviewedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task ApproveExtractedSpecificationAsync(int extractedSpecificationId, int? reviewedById = null)
    {
        var spec = await _context.AIExtractedSpecifications
            .FirstOrDefaultAsync(x => x.ExtractedSpecificationId == extractedSpecificationId);
        if (spec == null) throw new Exception("رکورد استخراج‌شده یافت نشد");

        spec.ReviewStatus = "Approved";
        spec.ReviewedById = reviewedById;
        spec.ReviewedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task RejectExtractedSpecificationAsync(int extractedSpecificationId, string? comment = null, int? reviewedById = null)
    {
        var spec = await _context.AIExtractedSpecifications
            .FirstOrDefaultAsync(x => x.ExtractedSpecificationId == extractedSpecificationId);
        if (spec == null) throw new Exception("رکورد استخراج‌شده یافت نشد");

        spec.ReviewStatus = "Rejected";
        spec.ReviewComment = comment;
        spec.ReviewedById = reviewedById;
        spec.ReviewedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    private static string ResolvePhysicalPath(string relativeFilePath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeFilePath.TrimStart('/', '\\'));
    }
}
