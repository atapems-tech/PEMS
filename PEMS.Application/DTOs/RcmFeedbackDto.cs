namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 7» بند ۲۰: نمایش یک توصیه‌ی بازبینی RCM - شواهد ساختاریافته + خلاصه‌ی خوانا،
/// هرگز یک تصمیم خودکار. Status فقط با اقدام صریح یک انسان (ReviewRecommendationAsync) تغییر
/// می‌کند.
/// </summary>
public class RcmReviewRecommendationDto
{
    public int RcmReviewRecommendationId { get; set; }

    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = string.Empty;

    public int MaintenanceStrategyId { get; set; }
    public string? MaintenanceStrategyType { get; set; }
    public string? FailureModeCode { get; set; }

    public int? RCMDecisionId { get; set; }

    public string TriggerReason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;

    public int? RepeatFailureCount { get; set; }
    public int? FailureCountInWindow { get; set; }
    public decimal? TotalDowntimeHoursInWindow { get; set; }
    public int WindowDays { get; set; }

    public string Status { get; set; } = "PENDING";
    public DateTime GeneratedDate { get; set; }

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}

/// <summary>
/// طبق بند ۲۰: نتیجه‌ی یک اجرای تولید شواهد - چند توصیه‌ی واقعاً جدید ساخته شد، چند استراتژی
/// به‌دلیل داشتن یک توصیه‌ی PENDING از قبل رد شد (بدون تکرار).
/// </summary>
public class GenerateEvidenceResultDto
{
    public int StrategiesEvaluated { get; set; }
    public int RecommendationsGenerated { get; set; }
    public int StrategiesSkippedAlreadyPending { get; set; }
    public List<string> GeneratedForTagNumbers { get; set; } = new();
}
