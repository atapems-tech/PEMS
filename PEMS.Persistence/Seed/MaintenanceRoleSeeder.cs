using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق درخواست بازنگری CMMS: نقش‌های پیش‌فرض تعمیرونگهداری برای دیسیپلین‌های کلاسیک
/// نگهداری (مکانیک، برق، ابزاردقیق، عمران) - اگر آن دیسیپلین در سیستم موجود باشد. سه سطح
/// برای هر کدام: تکنسین، تکنسین ارشد، سرگروه.
/// </summary>
public static class MaintenanceRoleSeeder
{
    private static readonly string[] MaintenanceDisciplineNames = { "Mechanical", "Electrical", "Instrumentation", "Civil" };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();


        var disciplines = await db.Disciplines
            .Where(x => x.IsActive && MaintenanceDisciplineNames.Contains(x.Name))
            .ToListAsync();

        if (disciplines.Count == 0) return;

        foreach (var discipline in disciplines)
        {
            // Phase 3B: Idempotent به‌ازای هر دیسیپلین (قبلاً گارد سراسری باعث می‌شد Instrumentation هرگز Seed نشود)؛ ردیف‌های موجود دست‌نخورده
            if (await db.MaintenanceRoles.AnyAsync(x => x.DisciplineId == discipline.DisciplineId)) continue;
            db.MaintenanceRoles.Add(new MaintenanceRole
            {
                Name = $"{discipline.Name} Technician",
                DisciplineId = discipline.DisciplineId,
                Level = "Junior",
                IsCrewLeadRole = false,
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            db.MaintenanceRoles.Add(new MaintenanceRole
            {
                Name = $"{discipline.Name} Senior Technician",
                DisciplineId = discipline.DisciplineId,
                Level = "Senior",
                IsCrewLeadRole = false,
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            db.MaintenanceRoles.Add(new MaintenanceRole
            {
                Name = $"{discipline.Name} Crew Leader",
                DisciplineId = discipline.DisciplineId,
                Level = "Lead",
                IsCrewLeadRole = true,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
