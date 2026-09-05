namespace PEMS.Application.DTOs;

/// <summary>
/// گزارش VDR - طبق درخواست: گزارش‌گیری از بخش‌های مختلف.
/// </summary>
public class VDRReportDto
{
    public int TotalItems { get; set; }
    public int ClosedCount { get; set; }
    public int UnderReviewCount { get; set; }
    public int PendingResubmissionCount { get; set; }
    public int OverdueCount { get; set; }
    public int TotalSubmissions { get; set; }

    public List<(string Month, int Count)> SubmissionsByMonth { get; set; } = new();
    public List<(string Vendor, int Count)> ByVendor { get; set; } = new();
    public List<(string Category, int Count)> ByReviewCategory { get; set; } = new();
    public List<(string Status, int Count)> ByWorkflowStatus { get; set; } = new();
}
