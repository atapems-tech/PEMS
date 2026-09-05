namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md بخش ۸ و ۲۷: هر تغییر وضعیت WorkOrder باید چه‌کسی/چه‌زمانی/
/// از‌چه‌به‌چه/چرا را ثبت کند. Append-only - هیچ Update/Delete روی رکورد قبلی نداریم، به همین
/// خاطر IsActive هم عمداً ندارد.
/// </summary>
public class WorkOrderStatusHistory
{
    public int WorkOrderStatusHistoryId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
