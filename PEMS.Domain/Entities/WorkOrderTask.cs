namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۴: چک‌لیست اجرای واقعی روی یک WorkOrder - وقتی از
/// یک MaintenancePlan دارای Job Plan یک WorkOrder ساخته می‌شود، ردیف‌های MaintenanceTask آن
/// Job Plan این‌جا کپی می‌شوند (هرکدام با IsCompleted=false)؛ تکنسین هنگام اجرا آن‌ها را
/// تیک می‌زند. جایگزین درست الگوی «همه‌چیز در یک فیلد Description» است.
/// </summary>
public class WorkOrderTask
{
    public int WorkOrderTaskId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int TaskNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsCompleted { get; set; }

    public string? CompletedBy { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
