using PEMS.Application.Common;

namespace PEMS.Web.ViewModels;

/// <summary>
/// مدل کمکی برای کامپوننت مشترک Pagination (_Pagination.cshtml). BaseUrl معمولاً همان
/// Request.Path است؛ QueryString فعلی (بدون پارامتر page/pageSize) حفظ می‌شود تا فیلترهای
/// دیگر هنگام تغییر صفحه/اندازه‌ی صفحه از بین نروند.
///
/// طبق سند «PEMS — Global Data Grid Standard»: PageSize/AllowedPageSizes/GridKey اختیاری‌اند
/// (پیش‌فرض صفر/خالی) تا Grid هایی که هنوز به استاندارد جدید مهاجرت نکرده‌اند (و صرفاً از
/// Page/TotalPages/TotalCount موجود استفاده می‌کنند) بدون تغییر کار کنند - این افزونه است،
/// نه یک شکست‌دهنده‌ی سازگاری.
/// </summary>
public class PaginationViewModel
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ExistingQueryString { get; set; } = "";

    // طبق بند ۴/۹ سند Data Grid Standard - وقتی GridKey پر باشد، _Pagination.cshtml علاوه بر
    // ناوبری صفحه، انتخاب‌گر Rows-per-page را هم رندر می‌کند.
    public string? GridKey { get; set; }
    public int PageSize { get; set; }
    public int[] AllowedPageSizes { get; set; } = DataGridPageSize.Allowed;

    public string BuildUrl(int page)
    {
        var qs = ExistingQueryString;
        return string.IsNullOrEmpty(qs) ? $"{BaseUrl}?page={page}" : $"{BaseUrl}?{qs}&page={page}";
    }

    // طبق بند ۱۰: تغییر اندازه‌ی صفحه باید Page را به ۱ بازنشانی کند
    public string BuildPageSizeUrl(int pageSize)
    {
        var qs = ExistingQueryString;
        return string.IsNullOrEmpty(qs)
            ? $"{BaseUrl}?pageSize={pageSize}&page=1"
            : $"{BaseUrl}?{qs}&pageSize={pageSize}&page=1";
    }
}
