namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-06: معیارهای قابلیت‌اطمینان یک تجهیز، محاسبه‌شده از روی
/// FailureRecord های واقعی (نه یک منبع مستقل - از همون داده‌ای که WorkOrderLifecycleService ثبت می‌کنه).
/// </summary>
public class EquipmentReliabilityDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string? EquipmentType { get; set; }

    public int FailureCount { get; set; }
    public DateTime? FirstFailureDate { get; set; }
    public DateTime? LastFailureDate { get; set; }

    // ساعت - Null یعنی داده‌ی کافی برای محاسبه نیست (کمتر از ۲ خرابی)
    public double? MTBFHours { get; set; }

    // ساعت - میانگین (CompletionDate - StartDate) دستورکارهای مرتبط
    public double? MTTRHours { get; set; }

    // درصد - MTBF / (MTBF + MTTR)
    public double? AvailabilityPercent { get; set; }

    public decimal TotalDowntimeHours { get; set; }

    public List<FailureHistoryItemDto> FailureHistory { get; set; } = new();
}

public class FailureHistoryItemDto
{
    public int FailureRecordId { get; set; }
    public DateTime FailureDate { get; set; }
    public string? FailureMode { get; set; }
    public string? FailureCause { get; set; }
    public string? FailureEffect { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public decimal? DowntimeHours { get; set; }
    public int? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
}

// ============================================================================================
// طبق «PEMS Phase 5 — Final Reliability Intelligence, Failure Analytics & RAM Foundation»:
// همه‌ی DTO های زیر روی همون منبع حقیقت موجود (FailureRecord، طبق بند بالا) و همون فرمول‌های
// از قبل تاییدشده‌ی ReliabilityService (MTBF نیازمند ≥۲ خرابی، MTTR فقط از WorkOrder های با
// StartDate/CompletionDate واقعی، Availability = MTBF/(MTBF+MTTR)) ساخته شده‌اند - نه یک مدل
// موازی. هیچ‌کدام مقدار را جعل نمی‌کنند: هرجا داده کافی نیست، Null می‌ماند (بند ۵۰: Zero ≠ Null).
// ============================================================================================

public class EquipmentClassReliabilityDto
{
    public int EquipmentClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int EquipmentPopulation { get; set; }
    public int EquipmentWithFailureHistory { get; set; }
    public int FailureCount { get; set; }
    public decimal TotalDowntimeHours { get; set; }

    // میانگین مقادیر معتبر سطح-تجهیز (نه یک نرخ Pooled روی زمان بهره‌برداری - چون PEMS فعلاً
    // تاریخ شروع بهره‌برداری قابل‌اتکا را برای محاسبه‌ی Exposure ذخیره نمی‌کند)
    public double? AverageMTBFHours { get; set; }
    public double? AverageMTTRHours { get; set; }
    public double? AverageAvailabilityPercent { get; set; }

    // طبق بند ۵۱: اندازه‌ی نمونه - چند تجهیز واقعاً در محاسبه‌ی میانگین بالا سهیم بودند
    public int MTBFSampleSize { get; set; }
    public int MTTRSampleSize { get; set; }
}

public class FailureParetoItemDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal DowntimeHours { get; set; }
    public double CumulativePercent { get; set; }
}

/// <summary>
/// طبق بند ۲۱: هر عامل رتبه‌بندی صریح و قابل‌توضیح است - نه یک فرمول پنهان یا امتیاز مبهم AI.
/// </summary>
public class BadActorDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string? EquipmentClassName { get; set; }
    public string? CriticalityClass { get; set; }

    public int FailureCount { get; set; }
    public decimal TotalDowntimeHours { get; set; }
    public double? MTBFHours { get; set; }

    public double RankScore { get; set; }
    public List<string> RankFactors { get; set; } = new();

    // ==============================================================================
    // طبق «PEMS Phase 7» بند ۱۶: گسترش Bad Actor موجود - فیلدهای زیر فقط زمانی پر می‌شوند که
    // داده‌ی واقعی موجود باشد (Null یعنی داده کافی نیست، نه صفر جعلی).
    // ==============================================================================
    public int? RepeatFailureCount { get; set; }
    public double? MTTRHours { get; set; }
    public int? MaintenanceCount { get; set; }
    public string? PrimaryFailureMode { get; set; }
    public string? PrimaryFailureCause { get; set; }
}

public class ReliabilityTrendPointDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public int FailureCount { get; set; }
    public decimal TotalDowntimeHours { get; set; }
}

/// <summary>
/// طبق بند ۳۵/۷۳: شفافیت کیفیت داده - نشان می‌دهد چه چیزی موجود است و چه چیزی غایب، بدون
/// تبدیل خودکار مقادیر غایب به صفر یا حدس.
/// </summary>
public class ReliabilityDataQualityDto
{
    public int TotalActiveEquipment { get; set; }
    public int EquipmentWithFailureHistory { get; set; }
    public int TotalFailureRecords { get; set; }
    public int FailureRecordsWithoutFailureMode { get; set; }
    public int FailureRecordsWithoutFailureCause { get; set; }
    public int FailureRecordsWithoutDowntimeHours { get; set; }
    public int FailureRecordsWithInvalidDate { get; set; }
    public int FailureRecordsLinkedToWorkOrderMissingDuration { get; set; }
    public int FailureRecordsWithNegativeDuration { get; set; }

    public double EquipmentReliabilityCoveragePercent { get; set; }
    public double FailureTaxonomyCoveragePercent { get; set; }
    public double RepairDataCoveragePercent { get; set; }

    // ==============================================================================
    // طبق «PEMS Phase 7» بند ۲۱: گسترش گیت کیفیت داده‌ی فاز ۵ - پوشش زنجیره‌ی نگهداری فراتر
    // از خودِ FailureRecord (Criticality/Strategy/Plan/WorkOrder/MaintenanceType). همه SQL-Aggregated.
    // ==============================================================================
    public int TotalWorkOrders { get; set; }
    public int WorkOrdersMissingWorkOrderType { get; set; }
    public int CompletedOrClosedWorkOrders { get; set; }
    public int WorkOrdersMissingCompletionDate { get; set; }

    public int EquipmentWithCriticality { get; set; }
    public int EquipmentWithMaintenanceStrategy { get; set; }
    public int EquipmentWithMaintenancePlan { get; set; }

    public double MaintenanceTypeCompletenessPercent { get; set; }
    public double WorkOrderCompletionCoveragePercent { get; set; }
    public double CriticalityCoveragePercent { get; set; }
    public double MaintenanceStrategyCoveragePercent { get; set; }
    public double MaintenancePlanCoveragePercent { get; set; }
}
