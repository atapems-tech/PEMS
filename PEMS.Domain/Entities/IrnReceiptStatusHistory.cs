namespace PEMS.Domain.Entities;

/// <summary>Append-only - مثل StoreRequisitionStatusHistory/WorkOrderStatusHistory.</summary>
public class IrnReceiptStatusHistory
{
    public int IrnReceiptStatusHistoryId { get; set; }

    public int IrnReceiptId { get; set; }
    public IrnReceipt? IrnReceipt { get; set; }

    public string FromStatus { get; set; } = string.Empty;

    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
