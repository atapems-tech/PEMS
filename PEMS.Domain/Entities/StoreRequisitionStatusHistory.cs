namespace PEMS.Domain.Entities;

/// <summary>Append-only - مثل WorkOrderStatusHistory.</summary>
public class StoreRequisitionStatusHistory
{
    public int StoreRequisitionStatusHistoryId { get; set; }

    public int StoreRequisitionId { get; set; }
    public StoreRequisition? StoreRequisition { get; set; }

    public string FromStatus { get; set; } = string.Empty;

    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }

    /// <summary>کلید Idempotency درخواست (اختیاری): تلاش مجدد با همان کلید دوباره اجرا نمی‌شود. یکتا به‌ازای هر درخواست.</summary>
    public string? RequestKey { get; set; }
}
