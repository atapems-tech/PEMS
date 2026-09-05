namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10a بند ۱۲: تاریخچه‌ی گذار وضعیت Permit - Append-only (بدون IsActive، هرگز
/// Update/Delete نمی‌شود)، دقیقاً همون شکل WorkOrderStatusHistory/WorkRequestStatusHistory.
/// </summary>
public class PermitStatusHistory
{
    public int PermitStatusHistoryId { get; set; }

    public int PermitId { get; set; }
    public Permit? Permit { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
