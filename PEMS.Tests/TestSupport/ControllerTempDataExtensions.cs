using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// اکشن‌هایی که TempData["Message"]/TempData["Error"] را ست می‌کنند، بدون HttpContext/
/// TempData واقعی با NullReferenceException می‌شکنند - چون ControllerBase.TempData خودش را
/// از HttpContext می‌سازد. این Extension یک TempData حداقلی (بدون Persist واقعی) فراهم می‌کند
/// که برای تست کافی است.
/// </summary>
public static class ControllerTempDataExtensions
{
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    public static T WithTempData<T>(this T controller) where T : Controller
    {
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
        return controller;
    }
}
