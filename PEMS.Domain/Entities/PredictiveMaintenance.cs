using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۶: زیرساخت نگهداری پیش‌بینانه. این رکورد فقط توصیف یک مدل احتمالی
/// است (هدف پیش‌بینی، الگوریتم توصیفی) - هیچ مدل واقعاً آموزش‌دیده/اعتبارسنجی‌شده‌ای پشت آن
/// نیست تا زمانی که ValidationStatus=VALIDATED باشد با شواهد واقعی. طبق بند ۱۶ سند: «No model
/// may be presented as production-validated without actual validation data.»
/// </summary>
public class PredictiveMaintenanceModel : BaseEntity
{
    [Key]
    public int PredictiveMaintenanceModelId { get; set; }

    public string TargetName { get; set; } = string.Empty;

    // توصیفی، نه ادعای یک کتابخانه‌ی ML واقعی - مثلاً "Weibull-based hazard projection"
    public string Algorithm { get; set; } = string.Empty;

    public string? Description { get; set; }

    // NOT_READY, READY - آیا داده‌ی آموزشی کافی وجود دارد
    public string TrainingDataStatus { get; set; } = "NOT_READY";

    // NOT_VALIDATED, VALIDATED - هرگز به VALIDATED تغییر داده نمی‌شود مگر با شواهد واقعی اعتبارسنجی
    public string ValidationStatus { get; set; } = "NOT_VALIDATED";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PredictiveMaintenanceResult> Results { get; set; } = new List<PredictiveMaintenanceResult>();
}

/// <summary>
/// نتیجه‌ی یک پیش‌بینی برای یک تجهیز مشخص. Provenance همیشه AI_SUGGESTED یا SIMULATED است -
/// هرگز OBSERVED/CALCULATED (چون این یک تخمین است، نه اندازه‌گیری واقعی).
/// </summary>
public class PredictiveMaintenanceResult
{
    [Key]
    public int PredictiveMaintenanceResultId { get; set; }

    public int PredictiveMaintenanceModelId { get; set; }
    public PredictiveMaintenanceModel? PredictiveMaintenanceModel { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public double? FailureProbability { get; set; }
    public double? RemainingUsefulLifeDays { get; set; }
    public double? AnomalyScore { get; set; }

    // AI_SUGGESTED, SIMULATED, ESTIMATED - هرگز OBSERVED/CALCULATED
    public string Provenance { get; set; } = "ESTIMATED";

    public string? Rationale { get; set; }

    public DateTime GeneratedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}
