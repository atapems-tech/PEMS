namespace PEMS.Application.DTOs;

/// <summary>
/// الگوی استاندارد Pagination برای کل برنامه - طبق توصیه‌ی گزارش ممیزی فیلترینگ
/// ("همان الگوی Audit Log که تنها صفحه‌ی دارای Pagination واقعی بود را به استاندارد کل
/// برنامه تبدیل کن"). هر سرویسی که فهرست بزرگ برمی‌گرداند باید از این استفاده کند،
/// نه یک Take() ثابت و بی‌صدا.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
