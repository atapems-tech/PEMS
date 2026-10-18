namespace PEMS.Web.Routing;

/// <summary>
/// ویژگی‌های انبار/مواد (Store Requisition، Material Master، IRN، Ledger، Master Data، MRS Import) به Area «Warehouse»
/// منتقل شدند. آدرس‌های قدیمی (/StoreRequisition/Details/5?x=1 ...) برای Bookmarkها و لینک‌های بیرونی همچنان کار می‌کنند:
/// فقط GET/HEAD، با ریدایرکت دائم به آدرس جدید (Query حفظ می‌شود). مقصد فقط از فهرست ثابت Controllerها ساخته
/// می‌شود و Action/Id با Encode وارد می‌شوند (بدون Open Redirect). POSTهای قدیمی عمداً Redirect نمی‌شوند
/// (فرم‌ها از صفحات جدید می‌آیند).
/// </summary>
public static class WarehouseLegacyRedirects
{
    public static readonly string[] Controllers =
    {
        "StoreRequisition", "MaterialItem", "IrnReceipt", "Inventory",
        "InventoryTransaction", "InventoryMasterData", "MrsImport"
    };

    public static string BuildTarget(string controller, string? action, string? id, string queryString)
    {
        var path = "/Warehouse/" + Uri.EscapeDataString(controller);
        if (!string.IsNullOrEmpty(action)) path += "/" + Uri.EscapeDataString(action);
        if (!string.IsNullOrEmpty(id)) path += "/" + Uri.EscapeDataString(id);
        return path + queryString;
    }

    public static IEndpointRouteBuilder MapWarehouseLegacyRedirects(this IEndpointRouteBuilder app)
    {
        var constraint = "^(" + string.Join("|", Controllers) + ")$";

        app.MapMethods("/{controller:regex(" + constraint + ")}/{action?}/{id?}", new[] { "GET", "HEAD" },
            (HttpContext ctx, string controller, string? action, string? id) =>
            {
                // نام Controller را از فهرست ثابت می‌گیریم (نه از ورودی خام) تا حروف بزرگ/کوچک هم کار کند
                var canonical = Controllers.First(c => string.Equals(c, controller, StringComparison.OrdinalIgnoreCase));
                return Results.Redirect(BuildTarget(canonical, action, id, ctx.Request.QueryString.Value ?? ""), permanent: true);
            }).AllowAnonymous();

        return app;
    }
}
