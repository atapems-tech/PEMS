using Hangfire.Dashboard;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Security;

/// <summary>
/// دسترسی به Hangfire Dashboard (/hangfire) فقط برای SystemAdmin - رصد صف پردازش‌های AI
/// (Intake Queue, OCR, VDR/MDR Import) و Background Job های دیگر.
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(SystemRoles.SystemAdmin);
    }
}
