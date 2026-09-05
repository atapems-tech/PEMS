namespace PEMS.Domain.Entities;

/// <summary>
/// طبق الگوی WorkOrderStatusHistory: Append-only، هیچ Update/Delete روی رکورد قبلی.
/// </summary>
public class WorkRequestStatusHistory
{
    public int WorkRequestStatusHistoryId { get; set; }

    public int WorkRequestId { get; set; }
    public WorkRequest? WorkRequest { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
