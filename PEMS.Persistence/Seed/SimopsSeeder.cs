using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق CMMS-10e بند ۴/۱۵ پلن: مقادیر SimopsInteractionRule پیش‌فرض‌های متعارف صنعتی هستند - در
/// انتظار تایید نهایی HSE، نه یک ماتریس خطر نهایی تایید‌شده.
/// </summary>
public static class SimopsSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        if (await db.SimopsInteractionRules.AnyAsync()) return;

        await db.SimopsInteractionRules.AddRangeAsync(
            new SimopsInteractionRule { PermitTypeA = "Hot Work", PermitTypeB = "Confined Space", Severity = "High", IsActive = true, CreatedDate = DateTime.Now },
            new SimopsInteractionRule { PermitTypeA = "Hot Work", PermitTypeB = "Line Breaking", Severity = "High", IsActive = true, CreatedDate = DateTime.Now },
            new SimopsInteractionRule { PermitTypeA = "Hot Work", PermitTypeB = "Excavation", Severity = "Medium", IsActive = true, CreatedDate = DateTime.Now },
            new SimopsInteractionRule { PermitTypeA = "Electrical", PermitTypeB = "Line Breaking", Severity = "Medium", IsActive = true, CreatedDate = DateTime.Now },
            new SimopsInteractionRule { PermitTypeA = "Radiography", PermitTypeB = "Hot Work", Severity = "High", IsActive = true, CreatedDate = DateTime.Now }
        );
        await db.SaveChangesAsync();
    }
}
