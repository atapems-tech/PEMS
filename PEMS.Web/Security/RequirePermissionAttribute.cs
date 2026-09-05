using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Security;

/// <summary>
/// اعمال Permission Matrix (نه فقط RBAC سطح Role) روی یک اکشن مشخص، مثلاً:
/// [RequirePermission("VDR", "Approve")]
/// SystemAdmin همیشه مجاز است. اگر هیچ ردیفی برای آن Role/Module در ماتریس نباشد، دسترسی رد می‌شود
/// (Deny by default) - این پایه‌ای است که ماژول‌های بعدی به‌مرور به آن مجهز می‌شوند.
/// </summary>
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _module;
    private readonly string _action;

    public RequirePermissionAttribute(string module, string action)
    {
        _module = module;
        _action = action;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var roles = user.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();

        var allowed = await permissionService.HasPermissionAsync(roles, _module, _action);

        if (!allowed)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
