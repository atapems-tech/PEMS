namespace PEMS.Application.DTOs;

// ============================================================================================
// طبق «PEMS Phase 7.5 — CMMS Operational Data Readiness & Master Data Activation» بند ۴۲:
// وضعیت‌های صریح، نه صفر/جعل. هر بُعد از آماده‌بودی دقیقاً یکی از این مقادیر را می‌گیرد:
//   READY               - داده‌ی معتبر موجود است
//   NOT_READY            - داده وجود ندارد و پیش‌نیاز دارد
//   MISSING_MASTER_DATA  - وابسته‌ای که این بُعد به آن نیاز دارد هنوز ساخته نشده
//   REQUIRES_REVIEW      - داده وجود دارد ولی نیاز به تایید/بازبینی انسانی دارد
//   BLOCKED_BY_SAFETY    - ایمنی هنوز پیکربندی نشده
//   NOT_APPLICABLE       - این بُعد در این مرحله قابل ارزیابی نیست (مثلاً JobPlan قبل از وجود Plan)
// ============================================================================================
public static class ReadinessState
{
    public const string Ready = "READY";
    public const string NotReady = "NOT_READY";
    public const string MissingMasterData = "MISSING_MASTER_DATA";
    public const string RequiresReview = "REQUIRES_REVIEW";
    public const string BlockedBySafety = "BLOCKED_BY_SAFETY";
    public const string NotApplicable = "NOT_APPLICABLE";
}

/// <summary>
/// طبق بند ۳۵: گزارش آماده‌بودی عملیاتی CMMS - هر عدد از COUNT واقعی SQL می‌آید، هیچ‌کدام جعل
/// نمی‌شود. اگر یک بُعد صفر است (مثلاً StrategyReady=0)، همان صفر واقعی نمایش داده می‌شود -
/// نه یک درصد ساختگی برای «به نظر رسیدن پیشرفت».
/// </summary>
public class CmmsOperationalReadinessDto
{
    public int TotalActiveEquipment { get; set; }
    public int ClassifiedEquipment { get; set; }
    public int UnclassifiedEquipment => TotalActiveEquipment - ClassifiedEquipment;

    public int CriticalityReadyEquipment { get; set; }
    public int StrategyReadyEquipment { get; set; }
    public int PmPlanReadyEquipment { get; set; }
    public int JobPlanReadyEquipment { get; set; }
    public int SafetyReadyEquipment { get; set; }
    public int ExecutionReadyEquipment { get; set; }
}

/// <summary>
/// طبق بند ۸/۹: کاندیدهای فعال‌سازی در سطح EquipmentClass، رتبه‌بندی‌شده با عوامل صریح
/// (نه یک امتیاز مبهم). این DTO روی IEquipmentClassCoverageService موجود (فاز ۳.۵) سوار
/// می‌شود - پوشش Activity/PMTemplate/JobPlan/FailureMode/Standard را از آن‌جا می‌گیرد، هرگز
/// دوباره محاسبه نمی‌کند.
/// </summary>
public class ClassActivationBacklogItemDto
{
    public int EquipmentClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int EquipmentCount { get; set; }
    public int ClassifiedIntoThisClassCount { get; set; }
    public int CriticalityReadyCount { get; set; }
    public int StrategyReadyCount { get; set; }
    public int PmPlanReadyCount { get; set; }
    public int ExecutionReadyCount { get; set; }

    // طبق بند ۸: بازاستفاده‌ی مستقیم از پوشش موجود کلاس (فاز ۳.۵) - نه یک محاسبه‌ی موازی
    public bool HasPMTemplateCoverage { get; set; }
    public bool HasJobPlanCoverage { get; set; }
    public bool HasFailureModeCoverage { get; set; }
    public bool HasMaintenanceActivityCoverage { get; set; }
    public bool HasStandardReferenceCoverage { get; set; }

    // طبق بند ۸: آیا این کلاس یک مبنای مدافعه‌پذیر (PMTemplate یا FailureMode) برای فعال‌سازی دارد؟
    public bool HasDefensibleBasis => HasPMTemplateCoverage || HasFailureModeCoverage;

    // طبق بند ۱۹/۲۶: عوامل رتبه‌بندی صریح - هر کدام دیده می‌شود، نه پنهان در یک فرمول
    public List<string> PriorityFactors { get; set; } = new();
    public List<string> MissingRequirements { get; set; } = new();
}

/// <summary>
/// طبق بند ۷: ماتریس آماده‌بودی سطح-تجهیز - هر بُعد صریح، قابل‌پرس‌وجو، صفحه‌بندی‌شده.
/// </summary>
public class EquipmentReadinessRowDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string? EquipmentClassName { get; set; }

    public string Classification { get; set; } = ReadinessState.NotReady;
    public string Criticality { get; set; } = ReadinessState.NotReady;
    public string Strategy { get; set; } = ReadinessState.NotReady;
    public string PmPlan { get; set; } = ReadinessState.NotReady;
    public string JobPlan { get; set; } = ReadinessState.NotApplicable;
    public string Safety { get; set; } = ReadinessState.NotApplicable;

    public bool ExecutionReady { get; set; }
}
