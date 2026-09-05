namespace PEMS.Application.DTOs;

/// <summary>
/// گزارش CMMS (Work Order/Maintenance) - طبق درخواست: هر بخش گزارش‌گیری مخصوص خودش.
/// </summary>
public class CmmsReportDto
{
    public int TotalWorkOrders { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int CriticalCount { get; set; }
    public double? AvgCompletionDays { get; set; }
    public decimal TotalSpareCost { get; set; }

    public List<(string Month, int Count)> VolumeByMonth { get; set; } = new();
    public List<(string Type, int Count)> ByType { get; set; } = new();
    public List<(string Priority, int Count)> ByPriority { get; set; } = new();
    public List<(string Equipment, int Count)> TopEquipment { get; set; } = new();
}
