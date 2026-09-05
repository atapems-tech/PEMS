namespace PEMS.Application.DTOs;

/// <summary>
/// طبق Phase E-03 (رفع P1 ممیزی Performance): فیلترهای فهرست مدارک مهندسی - همان چهار فیلتری
/// که امروز فقط سمت کلاینت (JavaScript) روی کل جدول اعمال می‌شوند، این‌جا مستقیم از QueryString
/// صفحه‌ی Index بایند و در SQL اعمال می‌شوند.
/// </summary>
public class EngineeringDocumentListFilter
{
    public string? Search { get; set; }
    public int? ProjectId { get; set; }
    public int? DisciplineId { get; set; }
    public string? Status { get; set; }
    public string? Area { get; set; }

    // طبق Phase E-05C (E-05B ADR-5): افزودنی برای Drilldown از فهرست کیفیت مدارک ("High
    // Revision Risk") - نه یک مکانیزم فیلتر موازی. OverdueOnly از E-05B اضافه نشد چون به مدل
    // Quality این فاز مربوط نیست (متعلق به Drilldown بخش Discipline/Overdue است، فاز دیگری).
    public int? MinRevisionCount { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
