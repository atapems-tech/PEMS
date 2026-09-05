namespace PEMS.Application.DTOs;

public class IntakeQueueItemDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string? ProjectName { get; set; }
    public string AIStatus { get; set; } = "";
    public string? IntakeStage { get; set; }
    public string? RoutedModule { get; set; }
    public DateTime UploadDate { get; set; }
    public double? AIConfidence { get; set; }
    public string? PredictedCategory { get; set; }
    public string? SuggestedTargetModule { get; set; }
}

public class IntakeDetailDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string AIStatus { get; set; } = "";
    public string? AIError { get; set; }
    public string? IntakeStage { get; set; }
    public string? RoutedModule { get; set; }
    public int? RoutedEntityId { get; set; }

    public int? ClassificationId { get; set; }
    public string? PredictedCategory { get; set; }
    public string? PredictedDocumentType { get; set; }
    public string? PredictedDisciplineCode { get; set; }
    public string? PredictedProjectHint { get; set; }
    public string? PredictedArea { get; set; }
    public string? PredictedUnit { get; set; }
    public string? PredictedSystem { get; set; }
    public string? PredictedSubsystem { get; set; }
    public string? PredictedRevisionNo { get; set; }
    public string? PredictedIssuePurpose { get; set; }
    public string? PredictedVendorName { get; set; }
    public string? PredictedMRNumber { get; set; }
    public string? PredictedEquipmentTagNumber { get; set; }
    public double ConfidenceScore { get; set; }
    public string ConfidenceLevel { get; set; } = "Low";
    public string? SuggestedTargetModule { get; set; }
    public string ReviewStatus { get; set; } = "Draft";
}

public class RoutingRuleDto
{
    public int RoutingRuleId { get; set; }
    public string DocumentCategory { get; set; } = "";
    public string TargetModule { get; set; } = "";
    public bool AllowAutoRouting { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class AIConfidenceSettingsDto
{
    public double HighThreshold { get; set; }
    public double MediumThreshold { get; set; }
}
