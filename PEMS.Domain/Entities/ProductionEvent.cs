namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش U/AN: تاریخچه‌ی رویدادهای
/// سبک تولید (append-only، بدون منطق Event Sourcing کامل) - مشابه الگوی موجود
/// WorkOrderStatusHistory/WorkflowHistory. هدف ردیابی قابل‌پیگیری است، نه بازسازی کامل حالت از
/// روی رویدادها.
/// </summary>
public class ProductionEvent
{
    public int ProductionEventId { get; set; }

    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int? ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    // OrderPlanned, OrderReleased, OrderHeld, OrderResumed, OrderCancelled, OrderCompleted, OrderClosed,
    // RunStarted, RunPaused, RunResumed, RunHeld, RunCompleted, RunCancelled,
    // InputConsumed, OutputRecorded, LossRecorded, BatchSubmittedForQC, BatchQualityStatusChanged
    public string EventType { get; set; } = string.Empty;

    public DateTime EventDate { get; set; } = DateTime.Now;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? Details { get; set; }
}
