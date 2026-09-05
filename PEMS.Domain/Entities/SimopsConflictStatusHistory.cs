namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10e: تاریخچه‌ی گذار وضعیت SimopsConflict - Append-only، دقیقاً همون شکل
/// GasTestStatusHistory/IsolationStatusHistory.
/// </summary>
public class SimopsConflictStatusHistory
{
    public int SimopsConflictStatusHistoryId { get; set; }

    public int SimopsConflictId { get; set; }
    public SimopsConflict? SimopsConflict { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
