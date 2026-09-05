namespace PEMS.Application.DTOs;

/// <summary>
/// گزارش تدارکات (MR/MRQ/Procurement Package) - طبق درخواست: هر بخش گزارش‌گیری مخصوص خودش.
/// </summary>
public class ProcurementReportDto
{
    public int TotalPackages { get; set; }
    public int TotalMRs { get; set; }
    public int TotalMRQs { get; set; }
    public int OpenCount { get; set; }
    public int ClosedCount { get; set; }

    public List<(string Month, int Count)> VolumeByMonth { get; set; } = new();
    public List<(string Status, int Count)> ByStatus { get; set; } = new();
    public List<(string Vendor, int Count)> ByVendor { get; set; } = new();
    public List<(string Discipline, int Count)> ByDiscipline { get; set; } = new();
}
