using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PEMS.Web.Security;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-03: قبلاً MustChangePassword فقط لحظه‌ی Login بررسی
/// می‌شد (AccountController.Login) - بعد از آن کاربر می‌توانست مستقیماً به هر URL دیگری برود و
/// این بررسی را دور بزند. این Filter به‌صورت سراسری (در Program.cs، بعد از AuthorizeFilter)
/// روی همه‌ی درخواست‌ها اجرا می‌شود.
/// AccountController کلاً استثنا است (نه فقط ChangePassword) چون Login/Logout/AccessDenied/
/// ForgotPassword هم باید همیشه در دسترس بمانند - وگرنه کاربر در یک حلقه‌ی Redirect گیر می‌کند.
/// </summary>
public class PasswordChangeGateFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (context.ActionDescriptor is ControllerActionDescriptor controllerAction &&
            controllerAction.ControllerName == "Account")
            return Task.CompletedTask;

        var mustChangePassword = user.Claims
            .FirstOrDefault(c => c.Type == "MustChangePassword")?.Value == "true";

        if (mustChangePassword)
            context.Result = new RedirectToActionResult("ChangePassword", "Account", null);

        return Task.CompletedTask;
    }
}
