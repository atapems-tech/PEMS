namespace PEMS.Application.DTOs;

public class AIDocumentClassificationDto
{
    public int AIDocumentClassificationId { get; set; }
    public int DocumentId { get; set; }
    public int AttachmentId { get; set; }
    public string? PredictedDocumentType { get; set; }
    public string? PredictedDisciplineCode { get; set; }
    public string? PredictedRevisionNo { get; set; }
    public string? PredictedIssuePurpose { get; set; }
    public string? PredictedVendorName { get; set; }
    public string? PredictedMRNumber { get; set; }
    public double ConfidenceScore { get; set; }
    public string ReviewStatus { get; set; } = "Draft";
}

public class EngineeringDocumentTagLinkDto
{
    public int EngineeringDocumentTagLinkId { get; set; }
    public int EquipmentTagId { get; set; }
    public string TagNumber { get; set; } = "";
    public bool IsAIProposed { get; set; }
    public string ReviewStatus { get; set; } = "Approved";
    public double? Confidence { get; set; }
}

public class EngineeringDocumentCommentDto
{
    public int EngineeringDocumentCommentId { get; set; }
    public string CommentText { get; set; } = "";
    public string? RaisedByName { get; set; }
    public DateTime RaisedDate { get; set; }
    public string ActionStatus { get; set; } = "OpenComment";
    public string? ResponseText { get; set; }
    public DateTime? ResponseDate { get; set; }
}

/// <summary>
/// نمای کامل Document Intelligence برای یک مدرک - صفحه‌ی Review طبق الگوی VDR.
/// </summary>
public class DocumentIntelligenceDetailDto
{
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string AIWorkflowStage { get; set; } = "";
    public string Status { get; set; } = "";

    // طبق PEMS_ENGINEERING_DOMAIN_BLUEPRINT_V2.md بخش ۴: DocumentStatus != AIProcessingStatus.
    // AIWorkflowStage بالا مراحل AI+انسانی رو با هم قاطی می‌کنه؛ این فیلد جدید مستقیماً از
    // DocumentAttachment.AIStatus (که خودش از قبل تمیز و درست پر می‌شه) خونده می‌شه - بدون
    // نیاز به ستون جدید روی خودِ Document.
    public string? AIProcessingStatus { get; set; }

    public List<AIDocumentClassificationDto> Classifications { get; set; } = new();
    public List<EngineeringDocumentTagLinkDto> TagLinks { get; set; } = new();
    public List<EngineeringDocumentCommentDto> Comments { get; set; } = new();
}

public class DocumentIntelligenceDashboardDto
{
    public int TotalDocuments { get; set; }
    public Dictionary<string, int> ByStage { get; set; } = new();
    public int PendingClassificationReview { get; set; }
    public int PendingTagLinkReview { get; set; }
    public int OpenComments { get; set; }
}
