using Microsoft.AspNetCore.Authorization;
using PEMS.Persistence.Identity;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۸ و تصمیم مجوزدهی مستندشده در
/// Areas/Admin/Controllers/HelpController: مدیریت محتوای راهنما فقط برای SystemAdmin/
/// EngineeringManager مجاز است (همان الگوی Areas/Admin/EmployeeController و
/// DepartmentController) - نه برای هر کاربر واردشده. [Authorize(Roles=...)] در سطح Action
/// توسط پایپ‌لاین واقعی MVC اجرا می‌شود، نه با فراخوانی مستقیم متد در تست؛ این تست پیکربندی
/// واقعیِ Attribute روی کنترلر را با Reflection تایید می‌کند - دقیقاً همان چیزی که در Runtime
/// توسط ASP.NET بررسی می‌شود.
/// </summary>
public class HelpAdminAuthorizationTests
{
    [Fact]
    public void AdminHelpController_RequiresSystemAdminOrEngineeringManagerRole()
    {
        var controllerType = typeof(PEMS.Web.Areas.Admin.Controllers.HelpController);

        var authorizeAttr = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.NotNull(authorizeAttr!.Roles);
        Assert.Contains(SystemRoles.SystemAdmin, authorizeAttr.Roles!.Split(','));
        Assert.Contains(SystemRoles.EngineeringManager, authorizeAttr.Roles!.Split(','));
    }

    [Fact]
    public void AdminHelpController_VisualManagementActions_ExistOnSameRoleProtectedController()
    {
        // طبق «PEMS Help System — Phase 3» بخش ۱۳: مدیریت محتوای بصری (VisualCreate/VisualEdit/
        // VisualSetPublished/VisualDelete) نباید یک کنترلر یا Attribute مجوز جداگانه داشته باشد -
        // چون همین [Authorize] در سطح کلاس (تایید‌شده در تست بالا) روی همه‌ی Action های این
        // کنترلر - از جمله این چهار مورد - به‌صورت خودکار اعمال می‌شود.
        var controllerType = typeof(PEMS.Web.Areas.Admin.Controllers.HelpController);
        var actionNames = new[] { "VisualCreate", "VisualEdit", "VisualSetPublished", "VisualDelete" };

        foreach (var name in actionNames)
        {
            var hasAction = controllerType.GetMethods().Any(m => m.Name == name);
            Assert.True(hasAction, $"Expected action '{name}' on Admin HelpController");
        }
    }

    [Fact]
    public void AdminHelpController_StandardReferenceManagementActions_ExistOnSameRoleProtectedController()
    {
        // طبق «PEMS Help Phase 4 — Increment 3A» بخش ۱۴: مدیریت Standard Reference هم دقیقاً
        // همان الگوی Visual را تکرار می‌کند - بدون کنترلر/Attribute مجوز جداگانه
        var controllerType = typeof(PEMS.Web.Areas.Admin.Controllers.HelpController);
        var actionNames = new[] { "StandardMatrix", "StandardCreate", "StandardEdit", "StandardSetPublished", "StandardDelete" };

        foreach (var name in actionNames)
        {
            var hasAction = controllerType.GetMethods().Any(m => m.Name == name);
            Assert.True(hasAction, $"Expected action '{name}' on Admin HelpController");
        }
    }

    [Fact]
    public void PublicHelpController_RequiresAuthenticatedUser_ButNoSpecificRole()
    {
        // طبق بخش ۹ سند: راهنما باید برای هر کاربر واردشده در دسترس باشد - نه محدودتر از
        // خودِ برنامه؛ فقط مدیریتِ محتوا (ساخت/ویرایش) محدود به نقش خاص است، نه مطالعه‌ی آن
        var controllerType = typeof(PEMS.Web.Controllers.HelpController);

        var authorizeAttr = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.True(string.IsNullOrEmpty(authorizeAttr!.Roles), "Public Help Center must not be role-restricted beyond authentication.");
    }
}
