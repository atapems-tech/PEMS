namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10e: تاریخچه‌ی گذار وضعیت ShiftHandover - Append-only، دقیقاً همون شکل
/// GasTestStatusHistory/SimopsConflictStatusHistory.
/// </summary>
public class ShiftHandoverStatusHistory
{
    public int ShiftHandoverStatusHistoryId { get; set; }

    public int ShiftHandoverId { get; set; }
    public ShiftHandover? ShiftHandover { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
