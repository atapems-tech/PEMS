namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۷ (AI Governance): این DTO همیشه ReviewStatus/ModelName/
/// SourceDataSummary را حمل می‌کند تا در UI هرگز یک پیشنهاد AI با ظاهر یک واقعیت اثبات‌شده
/// نمایش داده نشود.
/// </summary>
public class AIReliabilityInsightDto
{
    public int AIReliabilityInsightId { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public int? EquipmentClassId { get; set; }
    public string? EquipmentClassName { get; set; }
    public string InsightType { get; set; } = string.Empty;
    public string SuggestionText { get; set; } = string.Empty;
    public string SourceDataSummary { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime GeneratedDate { get; set; }
    public string Provenance { get; set; } = "AI_SUGGESTED";
}

public class GenerateInsightsResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int InsightsGenerated { get; set; }
    public string SourceDataSummary { get; set; } = string.Empty;
}
