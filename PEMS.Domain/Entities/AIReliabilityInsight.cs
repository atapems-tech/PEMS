using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۷ (AI Governance): همان الگوی تثبیت‌شده‌ی AIExtractedTag/
/// AIDocumentClassification (Confidence + ReviewStatus + ReviewedBy/ReviewedDate) این‌جا برای
/// بینش‌های قابلیت‌اطمینان بازاستفاده شده - نه یک الگوی جدید. AI فقط می‌تواند این ردیف‌ها را
/// با ReviewStatus="Draft" بسازد؛ هیچ کدی از این سرویس مستقیماً RCMDecision/MaintenanceStrategy/
/// EquipmentCriticality/FailureRecord/WorkOrder را نمی‌نویسد (بند ۱۷: تایید انسانی همیشه لازم
/// است). ModelName/GeneratedDate صریح ثبت می‌شوند تا Provenance=AI_SUGGESTED همیشه قابل ردیابی
/// به منبع باشد.
/// </summary>
public class AIReliabilityInsight : BaseEntity
{
    [Key]
    public int AIReliabilityInsightId { get; set; }

    // دامنه‌ی بینش - یا یک تجهیز مشخص یا یک کلاس تجهیز (هر دو می‌توانند Null باشند = ناوگانی)
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    // TrendSummary, CorrelationCandidate, InvestigationSuggestion, MaintenanceActionCandidate
    public string InsightType { get; set; } = string.Empty;

    public string SuggestionText { get; set; } = string.Empty;

    // خلاصه‌ی داده‌ی واقعی که این بینش از آن تولید شده (BadActors/Pareto/PmCompliance/...) -
    // برای ردیابی Provenance، نه یک ادعای بدون منبع
    public string SourceDataSummary { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    // مدل‌های زبانی محلی (Ollama) امتیاز اطمینان کالیبره‌شده برنمی‌گردانند - این فیلد عمداً
    // Null می‌ماند مگر یک مکانیزم امتیازدهی واقعی بعداً اضافه شود؛ هرگز جعل نمی‌شود.
    public double? Confidence { get; set; }

    // Draft, Approved, Rejected - طبق همان الگوی AIExtractedTag/AIDocumentClassification
    public string ReviewStatus { get; set; } = "Draft";

    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewComment { get; set; }

    public DateTime GeneratedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
