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
