using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Security;

/// <summary>
/// هنگام ورود کاربر، یک Claim اضافی برای نام کامل به هویت کاربر اضافه می‌کند
/// تا در Layout (سربرگ سایت) بدون کوئری اضافه به دیتابیس قابل نمایش باشد.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }


    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
        ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(
            "FullName",
            string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "" : user.FullName));

        // طبق CMMS Production Baseline v1 بند P0-03: این Claim توسط PasswordChangeGateFilter
        // در هر درخواست بررسی می‌شود، نه فقط لحظه‌ی Login. چون ChangePassword موفق باعث
        // RefreshSignInAsync می‌شود، این Claim خودش تازه می‌ماند - نیازی به کد اضافه نیست.
        identity.AddClaim(new Claim("MustChangePassword", user.MustChangePassword ? "true" : "false"));

        return identity;
    }
}
