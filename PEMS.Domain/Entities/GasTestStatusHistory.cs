namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10d: تاریخچه‌ی گذار وضعیت GasTest - Append-only، دقیقاً همون شکل
/// IsolationStatusHistory/PermitStatusHistory.
/// </summary>
public class GasTestStatusHistory
{
    public int GasTestStatusHistoryId { get; set; }

    public int GasTestId { get; set; }
    public GasTest? GasTest { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }
}
