using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Controllers;

/// <summary>
/// سوییچ Dark Mode / Light Mode - طبق Design System (بازبینی معماری، بند ۸).
/// فقط یک Cookie می‌سازد؛ _Layout.cshtml همان لحظه‌ی Render سمت سرور آن را می‌خواند
/// تا هیچ Flash محتوای بدون استایل (FOUC) هنگام تغییر تم رخ ندهد.
/// </summary>
public class ThemeController : Controller
{
    public const string CookieName = "pems_theme";

    [HttpGet]
    public IActionResult SetTheme(string theme, string returnUrl)
    {
        Response.Cookies.Append(CookieName, theme == "dark" ? "dark" : "light",
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }
}
