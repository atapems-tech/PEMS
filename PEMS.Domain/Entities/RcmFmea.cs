using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence» بند ۸-۱۸: این فایل فقط لایه‌ی
/// «تحلیل» (Analysis Data) را اضافه می‌کند - هرگز تاکسونومی خرابی فاز ۲ را تکرار نمی‌کند.
/// FMEAItem صرفاً یک ردیف پیوند است که FailureMode/Cause/Mechanism/Effect/Consequence
/// موجود (فاز ۲) را به یک تحلیل مشخص و یک ارزیابی FMECA وصل می‌کند - نه یک تاکسونومی جدید.
/// دامنه‌ی تحلیل عمداً EquipmentClass است (نه Equipment) طبق بند ۲۳ («Class First» - هرگز
/// ۲۱٬۶۲۵ تحلیل جدا ساخته نمی‌شود).
/// دامنه‌ی تصمیم RCM از MaintenanceStrategy (که در سطح Equipment، تراکنشی و از قبل موجود
/// است) عمداً جدا نگه داشته شده: RCMDecision یک «توصیه‌ی سطح کلاس» است که به مهندس کمک
/// می‌کند هنگام ساخت یک MaintenanceStrategy واقعی روی یک Equipment مشخص، تصمیم آگاهانه
/// بگیرد - هرگز خودکار یک MaintenanceStrategy نمی‌سازد (بند ۲۰: تایید انسانی الزامی است).
/// </summary>
public class FunctionalRequirement : BaseEntity
{
    public int FunctionalRequirementId { get; set; }

    public int EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RequiredFunction { get; set; }
    public string? PerformanceRequirement { get; set; }

    // طبق بند ۱۱: اگر مقدار مهندسی معتبری موجود نیست، Null می‌ماند - هرگز جعل نمی‌شود
    public decimal? MinimumPerformance { get; set; }
    public decimal? MaximumPerformance { get; set; }
    public string? Unit { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<FunctionalFailure> FunctionalFailures { get; set; } = new List<FunctionalFailure>();
}

public class FunctionalFailure : BaseEntity
{
    public int FunctionalFailureId { get; set; }

    public int FunctionalRequirementId { get; set; }
    public FunctionalRequirement? FunctionalRequirement { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FailureDefinition { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

/// <summary>
/// طبق بند ۱۳/۳۵: سند تحلیل FMEA سطح EquipmentClass - نسخه‌بندی‌شده و قابل‌ممیزی.
/// وضعیت‌ها: DRAFT → UNDER_REVIEW → APPROVED → SUPERSEDED/ARCHIVED (هرگز Overwrite مخرب -
/// یک نسخه‌ی تاییدشده هرگز حذف نمی‌شود، فقط با نسخه‌ی جدید SUPERSEDED علامت می‌خورد).
/// </summary>
public class FMEAAnalysis : BaseEntity
{
    public int FMEAAnalysisId { get; set; }

    public int EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Revision { get; set; } = 1;

    // DRAFT, UNDER_REVIEW, APPROVED, SUPERSEDED, ARCHIVED
    public string Status { get; set; } = "DRAFT";

    public DateTime? AnalysisDate { get; set; }

    // طبق بند ۱۳: از همان Identity/CurrentUser موجود پروژه استفاده می‌شود (رشته‌ی نام کاربری -
    // دقیقاً همان قرارداد EquipmentClassificationSuggestion.ReviewedBy فاز ۳)
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public int? SupersededByAnalysisId { get; set; }
    public FMEAAnalysis? SupersededByAnalysis { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<FMEAItem> Items { get; set; } = new List<FMEAItem>();
}

/// <summary>
/// طبق بند ۱۴/۲۷: یک ردیف تحلیل - Functional Failure را به تاکسونومی خرابی موجود (فاز ۲)
/// وصل می‌کند. Severity/Occurrence/Detectability طبق بند ۱۵ عمداً یک روش‌شناسی «پروژه‌ای و
/// قابل‌پیکربندی» است (۱ تا ۱۰، متداول در صنعت) - هرگز ادعا نمی‌شود که این روش یک الزام رسمی
/// ISO/SAE است؛ RiskPriorityNumber محاسبه‌شده (نه ذخیره‌شده) است تا هرگز Stale نشود.
/// </summary>
public class FMEAItem : BaseEntity
{
    public int FMEAItemId { get; set; }

    public int FMEAAnalysisId { get; set; }
    public FMEAAnalysis? FMEAAnalysis { get; set; }

    public int? FunctionalFailureId { get; set; }
    public FunctionalFailure? FunctionalFailure { get; set; }

    // طبق بند ۱۰: بازاستفاده‌ی مستقیم از تاکسونومی خرابی فاز ۲ - هرگز تکرار نشده
    public int FailureModeId { get; set; }
    public FailureMode? FailureMode { get; set; }

    public int? FailureCauseId { get; set; }
    public FailureCause? FailureCause { get; set; }

    public int? FailureMechanismId { get; set; }
    public FailureMechanism? FailureMechanism { get; set; }

    public int? FailureEffectId { get; set; }
    public FailureEffect? FailureEffect { get; set; }

    public int? FailureConsequenceId { get; set; }
    public FailureConsequence? FailureConsequence { get; set; }

    public string? DetectionMethod { get; set; }
    public string? ExistingControl { get; set; }
    public string? RecommendedAction { get; set; }

    // طبق بند ۱۵/۳۰: فقط وقتی یک مهندس واقعی این اعداد را وارد کرده معنا دارد - هرگز پیش‌فرض
    // جعل نمی‌شود (Null یعنی «هنوز ارزیابی نشده»، نه صفر)
    public int? Severity { get; set; }
    public int? Occurrence { get; set; }
    public int? Detectability { get; set; }

    public int? MaintenanceStrategyTypeId { get; set; }
    public MaintenanceStrategyType? MaintenanceStrategyType { get; set; }

    public int? StandardReferenceId { get; set; }
    public StandardReference? StandardReference { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // طبق بند ۱۵: RPN همیشه محاسبه‌شده است - هرگز به‌صورت مستقل ذخیره/Stale نمی‌شود.
    public int? RiskPriorityNumber => Severity.HasValue && Occurrence.HasValue && Detectability.HasValue
        ? Severity.Value * Occurrence.Value * Detectability.Value
        : null;
}

/// <summary>
/// طبق بند ۱۷/۲۸: لایه‌ی تصمیم RCM - سوار بر یک FMEAAnalysis تاییدشده (یا در حال بررسی).
/// آماده‌بودن RCM هرگز صرفاً به دلیل وجود FMEA اعلام نمی‌شود (بند ۲۸) - سرویس Readiness این
/// را جداگانه محاسبه می‌کند.
/// </summary>
public class RCMAnalysis : BaseEntity
{
    public int RCMAnalysisId { get; set; }

    public int EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    public int FMEAAnalysisId { get; set; }
    public FMEAAnalysis? FMEAAnalysis { get; set; }

    public int Revision { get; set; } = 1;

    // DRAFT, UNDER_REVIEW, APPROVED, SUPERSEDED, ARCHIVED
    public string Status { get; set; } = "DRAFT";

    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<RCMDecision> Decisions { get; set; } = new List<RCMDecision>();
}

/// <summary>
/// طبق بند ۱۸/۳۱: یک توصیه‌ی سطح کلاس («برای این Failure Mode، این نوع استراتژی توصیه
/// می‌شود») - هرگز جایگزین MaintenanceStrategy تراکنشی/سطح-Equipment موجود نیست و هرگز
/// خودکار آن را نمی‌سازد. مهندس هنگام ساخت یک MaintenanceStrategy واقعی روی یک Equipment
/// مشخص می‌تواند از این توصیه به‌عنوان مرجع استفاده کند.
/// </summary>
public class RCMDecision : BaseEntity
{
    public int RCMDecisionId { get; set; }

    public int RCMAnalysisId { get; set; }
    public RCMAnalysis? RCMAnalysis { get; set; }

    public int FMEAItemId { get; set; }
    public FMEAItem? FMEAItem { get; set; }

    // CONDITION_BASED, PREDICTIVE, TIME_BASED, USAGE_BASED, FAILURE_FINDING,
    // RUN_TO_FAILURE, REDESIGN, NO_SCHEDULED_MAINTENANCE, OTHER
    public string DecisionType { get; set; } = string.Empty;

    // طبق بند ۱۸: بازاستفاده‌ی مستقیم از MaintenanceStrategyType موجود (فاز ۱) - نه تاکسونومی جدید
    public int? MaintenanceStrategyTypeId { get; set; }
    public MaintenanceStrategyType? MaintenanceStrategyType { get; set; }

    // طبق بند ۳۲: هرگز یک PMTemplate جعلی ساخته نمی‌شود - فقط اگر یکی واقعاً موجود و مرتبط باشد پیوند می‌خورد
    public int? RecommendedPMTemplateId { get; set; }
    public PMTemplate? RecommendedPMTemplate { get; set; }

    public string? Rationale { get; set; }

    // Safety, Environmental, Operational, Economic - دقیقاً همان قرارداد
    // MaintenanceStrategy.ConsequenceCategory موجود (بدون تاکسونومی جدید)
    public string ConsequenceCategory { get; set; } = "Operational";

    // DRAFT, APPROVED
    public string Status { get; set; } = "DRAFT";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
