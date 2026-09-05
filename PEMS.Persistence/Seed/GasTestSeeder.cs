using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق CMMS-10d بند ۱۳ پلن: مقادیر GasParameterLimit پیش‌فرض‌های متعارف صنعتی هستند - در
/// انتظار تایید نهایی HSE، نه یک قاعده‌ی نهایی. مقدار GasTestValidityRule (۳۰ دقیقه، همه‌ی
/// انواع Permit) طبق تصمیم صریح کاربر هنگام تایید پلن Seed شده - نه حدس‌زده‌شده.
/// دو گارد مستقل (نه یک گارد کلی) - همون درس RoleModuleActionService.SeedModuleAsync.
/// </summary>
public static class GasTestSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        if (!await db.GasParameterLimits.AnyAsync())
        {
            await db.GasParameterLimits.AddRangeAsync(
                new GasParameterLimit { Parameter = "O2", Unit = "%VOL", MinValue = 19.5m, MaxValue = 23.5m, PermitType = null, IsActive = true, CreatedDate = DateTime.Now },
                new GasParameterLimit { Parameter = "LEL", Unit = "%LEL", MinValue = null, MaxValue = 10m, PermitType = null, IsActive = true, CreatedDate = DateTime.Now },
                new GasParameterLimit { Parameter = "H2S", Unit = "PPM", MinValue = null, MaxValue = 10m, PermitType = null, IsActive = true, CreatedDate = DateTime.Now },
                new GasParameterLimit { Parameter = "CO", Unit = "PPM", MinValue = null, MaxValue = 35m, PermitType = null, IsActive = true, CreatedDate = DateTime.Now }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.GasTestValidityRules.AnyAsync())
        {
            await db.GasTestValidityRules.AddAsync(
                new GasTestValidityRule { PermitType = null, ValidityMinutes = 30, IsActive = true, CreatedDate = DateTime.Now });
            await db.SaveChangesAsync();
        }
    }
}
