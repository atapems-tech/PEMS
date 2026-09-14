using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Controllers;

/// <summary>
/// سوییچ تم - طبق Design System (بازبینی معماری، بند ۸).
/// فقط یک Cookie می‌سازد؛ _Layout.cshtml همان لحظه‌ی Render سمت سرور آن را می‌خواند
/// تا هیچ Flash محتوای بدون استایل (FOUC) هنگام تغییر تم رخ ندهد.
///
/// طبق «PEMS Portal UX Azure-Inspired Audit» بند A: قبلاً این کنترلر فقط dark/light را
/// می‌شناخت، درحالی‌که site.css و منوی تم در _Layout.cshtml از ۵ تم (light/dark/ocean/warm/
/// midnight) پشتیبانی می‌کردند - یک مکانیزم سمت کلاینت جداگانه (admin.js، localStorage)
/// همان ۵ گزینه را مدیریت می‌کرد و هر بار رندر سمت سرور را بی‌صدا رونویسی می‌کرد. حالا این‌جا
/// تنها منبع حقیقت است؛ مکانیزم موازی حذف شده (admin.js).
/// </summary>
public class ThemeController : Controller
{
    public const string CookieName = "pems_theme";
    public const string DefaultTheme = "light";

    public static readonly string[] AllowedThemes = { "light", "dark", "ocean", "warm", "midnight" };

    public static string Normalize(string? theme) =>
        theme != null && Array.IndexOf(AllowedThemes, theme) >= 0 ? theme : DefaultTheme;

    [HttpGet]
    public IActionResult SetTheme(string theme, string returnUrl)
    {
        Response.Cookies.Append(CookieName, Normalize(theme),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl);
    }
}
