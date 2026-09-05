namespace PEMS.Application.DTOs;

/// <summary>
/// گزارش مدارک مهندسی/MDR - طبق درخواست: گزارش‌گیری از بخش‌های مختلف.
/// </summary>
public class EngineeringReportDto
{
    public int TotalDocuments { get; set; }
    public int ApprovedCount { get; set; }
    public int PendingCount { get; set; }
    public int RejectedCount { get; set; }
    public int IssuedCount { get; set; }
    public double? AvgDaysToApprove { get; set; }
    public int TotalRevisions { get; set; }
    public int OverdueCount { get; set; }

    public List<(string Month, int Created, int Issued)> VolumeByMonth { get; set; } = new();
    public List<(string Discipline, int Count)> ByDiscipline { get; set; } = new();
    public List<(string Status, int Count)> ByStatus { get; set; } = new();
    public List<(string ProjectName, int Count)> ByProject { get; set; } = new();
}
