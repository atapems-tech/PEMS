namespace PEMS.Application.DTOs;

/// <summary>
/// طبق Phase E-05C (E-05B §9 Quality Model): یک ردیف کیفیت مدرک - برای فهرست "High Revision
/// Risk" و محاسبه‌ی First Pass Approval Rate. FirstPassApproved قابل-Null است: null یعنی این
/// سند هنوز هیچ WorkflowInstance ای نداشته (هیچ‌وقت Submit نشده) - نه true و نه false، طبق
/// قانون "Missing نباید Zero/False جا زده شود".
/// </summary>
public class EngineeringRevisionQualityDto
{
    public int DocumentId { get; set; }
    public string DocumentNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string? DisciplineName { get; set; }
    public string? VendorName { get; set; }
    public string Status { get; set; } = "";

    // COUNT(*) - 1 روی DocumentRevisions فعال (منهای رویژن ۰۰ که همیشه ساخته می‌شود) - طبق E-05A §3
    public int RevisionCount { get; set; }

    // تعداد تصمیم‌های Reject در WorkflowHistory برای این سند (در همه‌ی Instance ها)
    public int RejectionCount { get; set; }

    // null = هنوز هیچ WorkflowInstance ای شروع نشده (Submit نشده) - نه true نه false
    public bool? FirstPassApproved { get; set; }
}
