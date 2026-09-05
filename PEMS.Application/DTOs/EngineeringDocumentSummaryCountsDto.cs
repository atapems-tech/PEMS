namespace PEMS.Application.DTOs;

/// <summary>
/// طبق Phase E-03: شمارش‌های نوار KPI بالای فهرست مدارک مهندسی - محاسبه‌شده در دیتابیس
/// (نه Model.Count روی کل لیست بارگذاری‌شده)، با همان محدودیت دیسیپلین که فهرست خودش دارد،
/// اما مستقل از فیلترهای جستجو/وضعیت/... (دقیقاً مثل رفتار فعلی که KPI ها با تایپ در جعبه‌ی
/// جستجوی سمت کلاینت تغییر نمی‌کردند).
/// </summary>
public class EngineeringDocumentSummaryCountsDto
{
    public int Total { get; set; }
    public int Approved { get; set; }
    public int Pending { get; set; }
    public int Rejected { get; set; }
    public int Frozen { get; set; }
    public int Overdue { get; set; }
}
