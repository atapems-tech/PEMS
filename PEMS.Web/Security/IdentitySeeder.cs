using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Security;

/// <summary>
/// در زمان استارت برنامه اجرا می‌شود:
///   1) نقش‌های استاندارد سیستم (SystemRoles) را در صورت نبودن می‌سازد
///   2) یک کاربر System Admin پیش‌فرض می‌سازد (فقط اگر هیچ کاربری در سیستم نباشد)
///
/// هشدار امنیتی: رمز عبور پیش‌فرض باید بلافاصله بعد از اولین ورود تغییر کند.
/// </summary>
public static class IdentitySeeder
{
    public const string DefaultAdminUserName = "admin";
    public const string DefaultAdminEmail = "admin@pems.local";
    public const string DefaultAdminPassword = "Admin@12345";


    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager =
            services.GetRequiredService<RoleManager<ApplicationRole>>();

        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        var logger =
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("IdentitySeeder");


        // =========================
        // 1) Seed Roles
        // =========================

        foreach (var roleName in SystemRoles.All)
        {
            var exists = await roleManager.RoleExistsAsync(roleName);

            if (exists)
                continue;

            var role = new ApplicationRole(roleName)
            {
                DisplayName = SystemRoles.DisplayNames.GetValueOrDefault(roleName, roleName)
            };

            var result = await roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "خطا در ساخت نقش {RoleName}: {Errors}",
                    roleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }


        // =========================
        // 2) Seed Default Admin (فقط اگر هیچ کاربری وجود ندارد)
        // =========================

        if (userManager.Users.Any())
            return;


        var adminUser = new ApplicationUser
        {
            UserName = DefaultAdminUserName,
            Email = DefaultAdminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            IsActive = true,
            // طبق ممیزی بند ۲۶: رمز پیش‌فرض مستندشده باید اجبار به تغییر داشته باشه - دقیقاً
            // همون الگویی که برای کاربران ساخته‌شده از Employee داریم
            MustChangePassword = true,
            CreatedDate = DateTime.Now
        };

        var createResult = await userManager.CreateAsync(
            adminUser,
            DefaultAdminPassword);

        if (!createResult.Succeeded)
        {
            logger.LogError(
                "خطا در ساخت کاربر ادمین پیش‌فرض: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));

            return;
        }

        await userManager.AddToRoleAsync(adminUser, SystemRoles.SystemAdmin);

        logger.LogWarning(
            "کاربر System Admin پیش‌فرض ساخته شد. Username: {UserName} — لطفاً بلافاصله بعد از اولین ورود رمز عبور پیش‌فرض را تغییر دهید.",
            DefaultAdminUserName);
    }
}
