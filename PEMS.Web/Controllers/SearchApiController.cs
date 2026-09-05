using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// ==============================================================================
/// Endpoint واحد Global Search Framework (فاز A). این کنترلر عمداً از GlobalSearchController
/// جداست - آن یکی یک صفحه‌ی کامل HTML نتایج جستجو است (/GlobalSearch?q=...)، این یکی یک
/// API سبک JSON برای کامپوننت Autocomplete/Typeahead مشترک (pems-global-search.js) است که
/// قراره جایگزین Dropdown های خام بشه. هیچ Controller دیگری در سیستم نباید Endpoint جستجوی
/// مخصوص خودش بسازد؛ همه از همین یک مسیر با پارامتر type استفاده می‌کنند.
/// ==============================================================================
/// </summary>
public class SearchApiController : Controller
{
    private readonly IGlobalSearchService _searchService;

    public SearchApiController(IGlobalSearchService searchService)
    {
        _searchService = searchService;
    }

    // GET /SearchApi?type=Equipment&q=PI&page=1&pageSize=20&projectId=
    [HttpGet]
    public async Task<IActionResult> Index(string type, string q, int page = 1, int pageSize = 20, int? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(q))
            return Json(new { entityType = type, query = q, page, pageSize, totalCount = 0, hasMore = false, items = Array.Empty<object>() });

        try
        {
            var result = await _searchService.SearchAsync(type, q, page, pageSize, projectId);
            return Json(result);
        }
        catch (InvalidOperationException ex)
        {
            // نوع موجودیت ثبت‌نشده - طبق اصل "بدون شکست خاموش"، خطای صریح ۴۰۰ برمی‌گردد
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /SearchApi/Entities - فهرست تمام موجودیت‌های ثبت‌شده (برای تست/مستندسازی/UI انتخاب دامنه)
    [HttpGet]
    public IActionResult Entities()
    {
        return Json(_searchService.GetRegisteredEntityTypes());
    }
}
