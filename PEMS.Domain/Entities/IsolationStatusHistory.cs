namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10c: تاریخچه‌ی گذار وضعیت IsolationPoint - Append-only، دقیقاً همون شکل
/// PermitStatusHistory/WorkOrderStatusHistory.
/// </summary>
public class IsolationStatusHistory
{
    public int IsolationStatusHistoryId { get; set; }

    public int IsolationPointId { get; set; }
    public IsolationPoint? IsolationPoint { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
