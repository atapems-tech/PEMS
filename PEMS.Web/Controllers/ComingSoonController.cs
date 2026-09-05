using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Controllers;

/// <summary>
/// صفحه‌ی موقت برای ماژول‌هایی که در چارت منو تعریف شده‌اند ولی هنوز پیاده‌سازی نشده‌اند
/// (طبق فاز فعلی پروژه: "ابتدا فقط Framework، بعد ماژول‌های Engineering").
/// این کنترلر باعث می‌شود منو کامل و قابل کلیک باشد بدون 404، تا زمانی که هر ماژول ساخته شود.
/// </summary>
public class ComingSoonController : Controller
{
    [HttpGet]
    public IActionResult Index(string title, string? section)
    {
        ViewData["Title"] = string.IsNullOrWhiteSpace(title) ? "Module" : title;

        ViewBag.Section = section;

        return View();
    }
}
