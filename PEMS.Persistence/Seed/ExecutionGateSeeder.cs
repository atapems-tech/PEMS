using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق CMMS-10f بند ۴/۱۶ پلن: مقادیر PermitExecutionRequirement پیش‌فرض‌های متعارف صنعتی هستند -
/// در انتظار تایید نهایی HSE، نه یک ماتریس نهایی تایید‌شده.
/// </summary>
public static class ExecutionGateSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        if (await db.PermitExecutionRequirements.AnyAsync()) return;

        await db.PermitExecutionRequirements.AddRangeAsync(
            new PermitExecutionRequirement { PermitType = "Confined Space", RequireIsolationVerified = true, RequireValidGasTest = true, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Hot Work", RequireIsolationVerified = false, RequireValidGasTest = true, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Line Breaking", RequireIsolationVerified = true, RequireValidGasTest = true, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Electrical", RequireIsolationVerified = true, RequireValidGasTest = false, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Cold Work", RequireIsolationVerified = false, RequireValidGasTest = false, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Working at Height", RequireIsolationVerified = false, RequireValidGasTest = false, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Excavation", RequireIsolationVerified = false, RequireValidGasTest = false, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Radiography", RequireIsolationVerified = false, RequireValidGasTest = false, IsActive = true, CreatedDate = DateTime.Now },
            new PermitExecutionRequirement { PermitType = "Other", RequireIsolationVerified = false, RequireValidGasTest = false, IsActive = true, CreatedDate = DateTime.Now }
        );
        await db.SaveChangesAsync();
    }
}
