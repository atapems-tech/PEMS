using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Controllers;

public class CultureController : Controller
{
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase) { "en", "fa" };

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult SetCulture(string culture, string? returnUrl = null)
    {
        var normalizedCulture = string.IsNullOrWhiteSpace(culture) ? "en" : culture.Trim().ToLowerInvariant();
        if (!SupportedCultures.Contains(normalizedCulture))
            normalizedCulture = "en";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture, normalizedCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

        // Prevent the culture-switch response and the following page from being restored
        // from browser history/BFCache with the previous direction.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        var target = Url.IsLocalUrl(returnUrl) && !string.IsNullOrWhiteSpace(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Home") ?? "/";

        // A normal HTTP redirect is intentional: the new request is processed by
        // UseRequestLocalization and the Layout then sees the new CurrentUICulture.
        return Redirect(target);
    }
}
