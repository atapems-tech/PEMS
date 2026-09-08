using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

public static class DisciplineSeeder
{
    private static readonly Discipline[] Defaults =
    {
        new() { Code = "MECH", Name = "Mechanical", Description = "Mechanical engineering discipline", ImportAliases = "ME, MEC, MECH ENG, Mechanical Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "ELEC", Name = "Electrical", Description = "Electrical engineering discipline", ImportAliases = "EL, ELEC, E&I, Electrical Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "INST", Name = "Instrumentation", Description = "Instrumentation and controls discipline", ImportAliases = "IN, INS, I&C, Instrumentation", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "PROC", Name = "Process", Description = "Process engineering discipline", ImportAliases = "PR, PRO, PROCESS, Process Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "PIP", Name = "Piping", Description = "Piping engineering discipline", ImportAliases = "PI, PIPING, Pipe, Piping Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "CIV", Name = "Civil", Description = "Civil and structural engineering discipline", ImportAliases = "CV, CIVIL, Structural, Civil Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "STR", Name = "Structural", Description = "Structural engineering discipline", ImportAliases = "ST, STRUCT, Structural Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "HSE", Name = "HSE", Description = "Health, safety and environment discipline", ImportAliases = "HS, SHE, SAFETY, HSE Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "ARCH", Name = "Architecture", Description = "Architectural engineering discipline", ImportAliases = "AR, ARCHITECTURE, Architectural", IsActive = true, CreatedDate = DateTime.Now },

        // ==============================================================================
        // طبق «PEMS — Enterprise Maintenance Master Data & Standards Implementation» بند ۴:
        // Maintenance Domain از Discipline موجود دوباره استفاده می‌کند (نه یک Entity جدید) -
        // این ۵ مورد در لیست اصلی نبودند و برای پوشش کامل Maintenance Domain اضافه شدند.
        // ==============================================================================
        new() { Code = "UTIL", Name = "Utilities", Description = "Utilities maintenance domain (steam, air, water, HVAC)", ImportAliases = "UTIL, Utility, Utilities Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "FIRE", Name = "Fire & Gas", Description = "Fire protection and fire & gas detection domain", ImportAliases = "FIRE, F&G, Fire Protection, Fire and Gas", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "INSP", Name = "Inspection", Description = "Inspection / Mechanical Integrity domain", ImportAliases = "INSP, MI, Mechanical Integrity, Inspection Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "CORR", Name = "Corrosion", Description = "Corrosion management domain", ImportAliases = "CORR, Corrosion Management, Corrosion Engineering", IsActive = true, CreatedDate = DateTime.Now },
        new() { Code = "REL", Name = "Reliability", Description = "Reliability engineering domain", ImportAliases = "REL, RCM, Reliability Engineering", IsActive = true, CreatedDate = DateTime.Now }
    };

    // ==============================================================================
    // طبق ممیزی «PEMS — Enterprise Maintenance Master Data»: گارد قبلی («اگر هر ردیفی هست کلاً
    // رد شو») روی دیتابیس‌های از قبل Seed‌شده (که تقریباً همه‌ی محیط‌های واقعی همینند) این متد
    // را برای همیشه بی‌اثر می‌کرد - همون کلاس باگی که قبلاً در PermissionService.SeedDefaultsAsync
    // پیدا و رفع شد. حالا مثل MaintenanceGroupSeeder، بررسی به‌ازای هر Code جداست - Additive،
    // Idempotent، بدون دست‌زدن به دیسیپلین‌هایی که کاربر خودش قبلاً ساخته/ویرایش کرده.
    // ==============================================================================
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var existingCodes = (await db.Disciplines.Select(x => x.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Defaults.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count == 0) return;

        db.Disciplines.AddRange(missing);
        await db.SaveChangesAsync();
    }
}
