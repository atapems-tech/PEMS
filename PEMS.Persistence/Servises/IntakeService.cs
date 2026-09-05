using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.AI.Interfaces;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی AI Intake Queue طبق سند AI Engineering Intelligence Redesign:
/// Upload → AI Classification → Confidence Gate → (خودکار یا با تایید انسانی) → Routing.
/// این سرویس هیچ‌وقت مستقیم و بدون طبقه‌بندی یک EngineeringDocument نمی‌سازد.
/// </summary>
public class IntakeService : IIntakeService
{
    private readonly PEMSDbContext _context;
    private readonly IDocumentAIService _documentAIService;
    private readonly IRoutingRuleService _routingRuleService;
    private readonly IVendorService _vendorService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IRelationshipService _relationshipService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<IntakeService> _logger;

    public IntakeService(
        PEMSDbContext context,
        IDocumentAIService documentAIService,
        IRoutingRuleService routingRuleService,
        IVendorService vendorService,
        IKnowledgeBaseService knowledgeBaseService,
        IRelationshipService relationshipService,
        IFileStorageService fileStorage,
        ILogger<IntakeService> logger)
    {
        _context = context;
        _documentAIService = documentAIService;
        _routingRuleService = routingRuleService;
        _vendorService = vendorService;
        _knowledgeBaseService = knowledgeBaseService;
        _relationshipService = relationshipService;
        _fileStorage = fileStorage;
        _logger = logger;
    }


    public async Task<int> UploadToIntakeAsync(Stream fileStream, string fileName, long fileSize, string? mimeType,
        int projectId, int? uploadedById)
    {
        var storageKey = await _fileStorage.SaveAsync("intake", fileName, fileStream);

        // Upload دیگر خودکار EngineeringDocument نمی‌سازد - فایل فقط این‌جا، بدون DocumentId، می‌نشیند
        var attachment = new DocumentAttachment
        {
            DocumentId = null,
            IntakeProjectId = projectId,
            IntakeStage = "Uploaded",
            FileName = fileName,
            FileExtension = Path.GetExtension(fileName),
            FilePath = storageKey,
            FileSize = fileSize,
            MimeType = mimeType,
            AIStatus = "Pending",
            UploadDate = DateTime.Now,
            // طبق بررسی قبلی: این پارامتر از قبل وجود داشت ولی هیچ‌جا واقعاً ذخیره نمی‌شد
            UploadedById = uploadedById,
            IsActive = true
        };

        await _context.DocumentAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();

        return attachment.AttachmentId;
    }


    public async Task ProcessIntakeAsync(int attachmentId)
    {
        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);

        if (attachment == null)
        {
            _logger.LogWarning("پیوست {AttachmentId} برای پردازش Intake یافت نشد", attachmentId);
            return;
        }

        attachment.AIStatus = "Processing";
        attachment.IntakeStage = "Classifying";
        await _context.SaveChangesAsync();

        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/', '\\'));

        if (!File.Exists(physicalPath))
        {
            attachment.AIStatus = "Failed";
            attachment.AIError = "فایل روی دیسک یافت نشد.";
            attachment.IntakeStage = "PendingManualClassification";
            attachment.AIProcessedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return;
        }

        var result = await _documentAIService.AnalyzeDocumentAsync(physicalPath, attachment.FileExtension);

        attachment.AIProcessedDate = DateTime.Now;

        if (!result.Success || result.ExtractedData?.DocumentInfo == null)
        {
            attachment.AIStatus = "Failed";
            attachment.AIError = result.ErrorMessage;
            attachment.IntakeStage = "PendingManualClassification";
            await _context.SaveChangesAsync();
            return;
        }

        attachment.AIStatus = "Completed";
        attachment.AIError = null;
        attachment.AISummary = result.Summary;
        attachment.AIConfidence = result.Confidence;

        var info = result.ExtractedData.DocumentInfo;
        var category = string.IsNullOrWhiteSpace(info.Category) ? "Other" : info.Category!;

        var settings = await _routingRuleService.GetConfidenceSettingsAsync();

        var confidenceLevel = result.Confidence >= settings.HighThreshold ? "High"
            : result.Confidence >= settings.MediumThreshold ? "Medium"
            : "Low";

        var suggestedModule = await _routingRuleService.GetTargetModuleAsync(category);

        var oldDraft = await _context.AIDocumentClassifications
            .Where(x => x.AttachmentId == attachmentId && x.ReviewStatus == "Draft")
            .ToListAsync();
        _context.AIDocumentClassifications.RemoveRange(oldDraft);

        var classification = new AIDocumentClassification
        {
            AttachmentId = attachmentId,
            PredictedCategory = category,
            PredictedDocumentType = info.DocumentType,
            PredictedDisciplineCode = info.Discipline,
            PredictedProjectHint = info.ProjectHint,
            PredictedArea = info.Area,
            PredictedUnit = info.Unit,
            PredictedSystem = info.System,
            PredictedSubsystem = info.Subsystem,
            PredictedRevisionNo = info.RevisionNo,
            PredictedIssuePurpose = info.IssuePurpose,
            PredictedVendorName = info.VendorName,
            PredictedMRNumber = info.RelatedMRNumber,
            PredictedEquipmentTagNumber = result.ExtractedData.EquipmentTags.FirstOrDefault()?.TagNumber,
            ConfidenceScore = result.Confidence,
            ConfidenceLevel = confidenceLevel,
            SuggestedTargetModule = suggestedModule,
            ReviewStatus = "Draft",
            CreatedDate = DateTime.Now
        };

        await _context.AIDocumentClassifications.AddAsync(classification);
        await _context.SaveChangesAsync();

        // Knowledge Base Indexing: خلاصه‌ی AI به همراه فراداده‌ی کلیدی، برای Semantic Search آینده
        var kbContent = $"{attachment.FileName}: {result.Summary} " +
            $"[Category: {category}, Discipline: {info.Discipline}, Vendor: {info.VendorName}, " +
            $"Area: {info.Area}, Equipment: {classification.PredictedEquipmentTagNumber}]";

        await _knowledgeBaseService.IndexAsync("Attachment", attachmentId, kbContent);

        var allowAuto = await _routingRuleService.AllowsAutoRoutingAsync(category);

        if (confidenceLevel == "High" && allowAuto &&
            (suggestedModule == "MDR" || suggestedModule == "VDR") &&
            !string.IsNullOrWhiteSpace(info.DocumentNo))
        {
            try
            {
                await RouteAsync(attachmentId, suggestedModule!, category, info.DocumentType,
                    info.Discipline, attachment.IntakeProjectId, info.Area, info.RevisionNo,
                    info.IssuePurpose, info.VendorName, info.RelatedMRNumber, null);

                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-routing پیوست {AttachmentId} ناموفق بود؛ به بازبینی دستی منتقل شد", attachmentId);
            }
        }

        attachment.IntakeStage = confidenceLevel == "Medium" ? "PendingEngineeringReview" : "PendingManualClassification";

        await _context.SaveChangesAsync();
    }


    public async Task<PagedResult<IntakeQueueItemDto>> GetQueueAsync(string? stage = null, int page = 1, int pageSize = 50)
    {
        var query = _context.DocumentAttachments
            .AsNoTracking()
            .Include(x => x.IntakeProject)
            .Where(x => x.IntakeStage != null && x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(stage))
            query = query.Where(x => x.IntakeStage == stage);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        // Defense-in-depth طبق سند Global Data Grid Standard - حتی اگر Controller قبلاً
        // Normalize کرده باشد، Service هم دوباره فقط ۲۰/۵۰/۱۰۰ را می‌پذیرد.
        pageSize = DataGridPageSize.Normalize(pageSize);

        var attachments = await query
            .OrderByDescending(x => x.UploadDate)
            .ThenByDescending(x => x.AttachmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var attachmentIds = attachments.Select(x => x.AttachmentId).ToList();

        var latestClassifications = await _context.AIDocumentClassifications
            .Where(x => attachmentIds.Contains(x.AttachmentId))
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var items = attachments.Select(a =>
        {
            var c = latestClassifications.FirstOrDefault(x => x.AttachmentId == a.AttachmentId);

            return new IntakeQueueItemDto
            {
                AttachmentId = a.AttachmentId,
                FileName = a.FileName,
                ProjectName = a.IntakeProject?.ProjectName,
                AIStatus = a.AIStatus ?? "Pending",
                IntakeStage = a.IntakeStage,
                RoutedModule = a.RoutedModule,
                UploadDate = a.UploadDate,
                AIConfidence = a.AIConfidence,
                PredictedCategory = c?.PredictedCategory,
                SuggestedTargetModule = c?.SuggestedTargetModule
            };
        }).ToList();

        return new PagedResult<IntakeQueueItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }


    public async Task<IntakeDetailDto?> GetDetailAsync(int attachmentId)
    {
        var attachment = await _context.DocumentAttachments
            .Include(x => x.IntakeProject)
            .FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);

        if (attachment == null) return null;

        var classification = await _context.AIDocumentClassifications
            .Where(x => x.AttachmentId == attachmentId)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync();

        return new IntakeDetailDto
        {
            AttachmentId = attachment.AttachmentId,
            FileName = attachment.FileName,
            ProjectId = attachment.IntakeProjectId,
            ProjectName = attachment.IntakeProject?.ProjectName,
            AIStatus = attachment.AIStatus ?? "Pending",
            AIError = attachment.AIError,
            IntakeStage = attachment.IntakeStage,
            RoutedModule = attachment.RoutedModule,
            RoutedEntityId = attachment.RoutedEntityId,

            ClassificationId = classification?.AIDocumentClassificationId,
            PredictedCategory = classification?.PredictedCategory,
            PredictedDocumentType = classification?.PredictedDocumentType,
            PredictedDisciplineCode = classification?.PredictedDisciplineCode,
            PredictedProjectHint = classification?.PredictedProjectHint,
            PredictedArea = classification?.PredictedArea,
            PredictedUnit = classification?.PredictedUnit,
            PredictedSystem = classification?.PredictedSystem,
            PredictedSubsystem = classification?.PredictedSubsystem,
            PredictedRevisionNo = classification?.PredictedRevisionNo,
            PredictedIssuePurpose = classification?.PredictedIssuePurpose,
            PredictedVendorName = classification?.PredictedVendorName,
            PredictedMRNumber = classification?.PredictedMRNumber,
            PredictedEquipmentTagNumber = classification?.PredictedEquipmentTagNumber,
            ConfidenceScore = classification?.ConfidenceScore ?? 0,
            ConfidenceLevel = classification?.ConfidenceLevel ?? "Low",
            SuggestedTargetModule = classification?.SuggestedTargetModule,
            ReviewStatus = classification?.ReviewStatus ?? "Draft"
        };
    }


    public async Task<(string module, int entityId)> RouteAsync(int attachmentId, string targetModule,
        string? category, string? documentType, string? disciplineCode, int? projectId, string? area,
        string? revisionNo, string? issuePurpose, string? vendorName, string? mrNumber, int? reviewedById)
    {
        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);

        if (attachment == null) throw new Exception("پیوست یافت نشد");

        var resolvedProjectId = projectId ?? attachment.IntakeProjectId
            ?? throw new Exception("پروژه مشخص نیست");

        int? disciplineId = null;

        if (!string.IsNullOrWhiteSpace(disciplineCode))
        {
            var discipline = await _context.Disciplines
                .FirstOrDefaultAsync(x => x.Code.ToLower() == disciplineCode.ToLower());

            disciplineId = discipline?.DisciplineId;
        }

        int entityId;

        if (targetModule == "MDR")
        {
            var docNo = string.IsNullOrWhiteSpace(attachment.FileName)
                ? "AI-" + DateTime.Now.ToString("yyyyMMddHHmmss")
                : Path.GetFileNameWithoutExtension(attachment.FileName);

            var doc = new EngineeringDocument
            {
                ProjectId = resolvedProjectId,
                DisciplineId = disciplineId,
                DocumentNo = docNo,
                Title = docNo,
                DocumentType = string.IsNullOrWhiteSpace(documentType) ? "Other" : documentType,
                Area = area,
                RevisionNo = string.IsNullOrWhiteSpace(revisionNo) ? "00" : revisionNo,
                IssuePurpose = issuePurpose,
                Status = "Draft",
                AIWorkflowStage = "Classified",
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            await _context.EngineeringDocuments.AddAsync(doc);
            await _context.SaveChangesAsync();

            attachment.DocumentId = doc.DocumentId;
            entityId = doc.DocumentId;
        }
        else if (targetModule == "VDR")
        {
            var vendorId = await _vendorService.FindOrCreateByNameAsync(
                string.IsNullOrWhiteSpace(vendorName) ? "Unknown Vendor" : vendorName!);

            var docNo = Path.GetFileNameWithoutExtension(attachment.FileName);

            var vdrItem = new VDRItem
            {
                ProjectId = resolvedProjectId,
                DisciplineId = disciplineId,
                VendorId = vendorId,
                DocumentNumber = docNo,
                DocumentDescription = docNo,
                DocumentTypeCode = documentType ?? "",
                WorkflowStatus = "Submitted",
                DocumentKind = "Vendor",
                DocumentNo = docNo,
                Title = docNo,
                DocumentType = documentType ?? "",
                Area = area,
                Status = "Submitted",
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            await _context.VDRItems.AddAsync(vdrItem);
            await _context.SaveChangesAsync();

            await _context.VDRSubmissions.AddAsync(new VDRSubmission
            {
                VDRItemId = vdrItem.DocumentId,
                RevisionNo = 0,
                AttachmentId = attachmentId,
                VendorActualIssuedDate = DateTime.Now,
                CreatedDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            entityId = vdrItem.DocumentId;
        }
        else
        {
            // MR, MRQ, Contract, CMMS, Unrouted: هنوز موتور ساخت خودکار رکورد کامل نداریم؛
            // فقط برچسب‌گذاری می‌شود تا کاربر آن را در ماژول درست، با فایل به‌عنوان مرجع، تکمیل کند
            entityId = 0;
        }

        attachment.RoutedModule = targetModule;
        attachment.RoutedEntityId = entityId > 0 ? entityId : null;
        attachment.IntakeStage = "Routed";

        var classification = await _context.AIDocumentClassifications
            .Where(x => x.AttachmentId == attachmentId && x.ReviewStatus == "Draft")
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync();

        if (classification != null)
        {
            classification.ReviewStatus = "Approved";
            classification.SuggestedTargetModule = targetModule;
            classification.DocumentId = targetModule == "MDR" ? entityId : null;
            classification.ReviewedById = reviewedById;
            classification.ReviewedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        // Knowledge Graph: اگر مدرک واقعاً Route شد (MDR/VDR)، متن خلاصه‌ی AI را برای ارجاع به
        // مدارک/تجهیزات دیگر همان پروژه اسکن می‌کنیم و روابط کشف‌شده را ثبت می‌کنیم - طبق
        // AI Relationship Engine (فقط برای FK های صریح موجود کافی نبود)
        if (entityId > 0 && (targetModule == "MDR" || targetModule == "VDR"))
        {
            try
            {
                await DetectAndLinkRelationshipsAsync(entityId, resolvedProjectId, attachment.AISummary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "کشف خودکار روابط برای مدرک {EntityId} ناموفق بود", entityId);
            }
        }

        return (targetModule, entityId);
    }


    private async Task DetectAndLinkRelationshipsAsync(int documentId, int projectId, string? summaryText)
    {
        if (string.IsNullOrWhiteSpace(summaryText))
            return;

        var otherDocs = await _context.EngineeringDocuments
            .Where(x => x.ProjectId == projectId && x.DocumentId != documentId && x.IsActive)
            .Select(x => new { x.DocumentId, x.DocumentNo })
            .ToListAsync();

        var otherVdrItems = await _context.VDRItems
            .Where(x => x.ProjectId == projectId && x.DocumentId != documentId && x.IsActive)
            .Select(x => new { x.DocumentId, x.DocumentNumber })
            .ToListAsync();

        var equipmentList = await _context.Equipments
            .Where(x => x.ProjectId == projectId && x.IsActive)
            .Select(x => new { x.EquipmentId, x.TagNumber })
            .ToListAsync();

        foreach (var doc in otherDocs)
        {
            if (!string.IsNullOrWhiteSpace(doc.DocumentNo) &&
                summaryText.Contains(doc.DocumentNo, StringComparison.OrdinalIgnoreCase))
            {
                await _relationshipService.AddAIProposedRelationshipAsync(
                    "Document", documentId, "Document", doc.DocumentId, "References", 0.6);
            }
        }

        foreach (var vdr in otherVdrItems)
        {
            if (!string.IsNullOrWhiteSpace(vdr.DocumentNumber) &&
                summaryText.Contains(vdr.DocumentNumber, StringComparison.OrdinalIgnoreCase))
            {
                await _relationshipService.AddAIProposedRelationshipAsync(
                    "Document", documentId, "Document", vdr.DocumentId, "References", 0.6);
            }
        }

        foreach (var eq in equipmentList)
        {
            if (!string.IsNullOrWhiteSpace(eq.TagNumber) &&
                summaryText.Contains(eq.TagNumber, StringComparison.OrdinalIgnoreCase))
            {
                await _relationshipService.AddAIProposedRelationshipAsync(
                    "Document", documentId, "Equipment", eq.EquipmentId, "References", 0.7);
            }
        }
    }

    public async Task RejectAsync(int attachmentId, int? reviewedById)
    {
        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);
        if (attachment == null) return;

        attachment.IntakeStage = "Rejected";

        var classification = await _context.AIDocumentClassifications
            .Where(x => x.AttachmentId == attachmentId && x.ReviewStatus == "Draft")
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync();

        if (classification != null)
        {
            classification.ReviewStatus = "Rejected";
            classification.ReviewedById = reviewedById;
            classification.ReviewedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }
}
