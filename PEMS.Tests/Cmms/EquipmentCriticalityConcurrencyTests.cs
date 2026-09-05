using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B Wave A — ممیزی مستقل نهایی» بند P1-02: خواندن existing قبل از Insert در
/// برابر Race Condition امن نیست. این تست روی همون دیتابیس SQL Server ایزوله
/// (PEMSDB_ConcurrencyTest، همون الگوی TransmittalNumberingConcurrencyTests/
/// MaterialRequisitionMrNumberConcurrencyTests) اجرا می‌شود - ساخته‌شده از روی تاریخچه‌ی
/// Migration واقعی، پس ایندکس Unique فیلترشده‌ی واقعی
/// (IX_EquipmentCriticalities_EquipmentId، WHERE IsActive=1) را دارد. نقش SystemAdmin برای
/// عبور از گیت مجوز استفاده می‌شود (Bypass بدون نیاز به Seed کردن Permission/RolePermissionGrant
/// در دیتابیس ایزوله - طبق AccessControlService.DecideAsync).
/// </summary>
public class EquipmentCriticalityConcurrencyTests
{
    private const string IsolatedDbName = "PEMSDB_ConcurrencyTest";

    private static EquipmentCriticalityController CreateController(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        var currentUser = new FakeCurrentUserService { Roles = new List<string> { SystemRoles.SystemAdmin } };
        return new EquipmentCriticalityController(ctx, permissionService, currentUser).WithTempData();
    }

    [Fact]
    public async Task Assess_TenConcurrentRequestsSameEquipment_ExactlyOneActiveRecordPersisted()
    {
        int equipmentId, projectId, disciplineId;

        // ==== Seed: Project/Discipline/Equipment واقعی در دیتابیس ایزوله (نه در PEMSDB) ====
        await using (var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var project = new Project { ProjectName = "EqCrit Concurrency Project", ProjectCode = $"CT-{Guid.NewGuid():N}"[..10], IsActive = true };
            var discipline = new Discipline { Name = "EqCrit Concurrency Discipline", Code = $"CD{Guid.NewGuid():N}"[..6], IsActive = true };
            seedCtx.Projects.Add(project);
            seedCtx.Disciplines.Add(discipline);
            await seedCtx.SaveChangesAsync();

            var equipment = new Equipment
            {
                TagNumber = $"EQ-{Guid.NewGuid():N}"[..12],
                Name = "Concurrency Test Equipment",
                EquipmentType = "Pump",
                ProjectId = project.ProjectId,
                DisciplineId = discipline.DisciplineId,
                IsActive = true
            };
            seedCtx.Equipments.Add(equipment);
            await seedCtx.SaveChangesAsync();

            equipmentId = equipment.EquipmentId;
            projectId = project.ProjectId;
            disciplineId = discipline.DisciplineId;
        }

        try
        {
            // ==== Race: ۱۰ تلاش هم‌زمان برای همون Equipment ====
            // طبق ممیزی: صرفاً Task.WhenAll ده فراخوانی controller.Assess کافی نیست - چون این
            // عملیات semantics «جایگزینی» دارد (نه صرفاً INSERT مثل MRNumber/TransmittalNo):
            // اگر ۱۰ درخواست کاملاً پشت‌سرهم (بدون همپوشانی واقعی) اجرا شوند، هرکدام existing
            // را از رکورد قبلی می‌بیند، آن را غیرفعال می‌کند، و رکورد خودش را فعال می‌کند - که
            // یک دنباله‌ی معتبر و متوالی است، نه یک Race واقعی. برای اثبات واقعی «حداکثر یک
            // رکورد فعال حتی زیر Overlap واقعی»، این‌جا با Barrier مرحله‌ی خواندن (existing) و
            // مرحله‌ی نوشتن (Deactivate+Insert+SaveChanges) را برای همه‌ی شرکت‌کننده‌ها هم‌زمان
            // می‌کنیم - دقیقاً همون منطق controller.Assess (خط‌به‌خط)، فقط با تضمین Overlap واقعی.
            const int concurrentRequests = 10;
            using var readBarrier = new Barrier(concurrentRequests);

            var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);

                var existing = await ctx.EquipmentCriticalities
                    .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive);

                // همه‌ی ۱۰ شرکت‌کننده تا این‌جا صبر می‌کنند تا مطمئن شویم همه existing را قبل از
                // نوشتن هر کدام خوانده‌اند - همون پنجره‌ی Race واقعی.
                readBarrier.SignalAndWait();

                if (existing != null) existing.IsActive = false;

                ctx.EquipmentCriticalities.Add(new EquipmentCriticality
                {
                    EquipmentId = equipmentId,
                    CriticalityClass = "A",
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });

                try
                {
                    await ctx.SaveChangesAsync();
                    return (Won: true, Error: (string?)null);
                }
                catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
                    && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    return (Won: false, Error: ex.Message);
                }
            });

            var results = await Task.WhenAll(tasks);

            // ==== Assertion 1: حداقل یک برنده و حداقل یک بازنده‌ی کنترل‌شده (نه Exception خام) ====
            var winners = results.Where(r => r.Won).ToList();
            var losers = results.Where(r => !r.Won).ToList();

            Assert.NotEmpty(winners);
            Assert.NotEmpty(losers);
            Assert.All(losers, l => Assert.False(string.IsNullOrWhiteSpace(l.Error)));

            // ==== Assertion 2 (تضمین اصلی): صحت دیتابیس - دقیقاً یک رکورد فعال برای این Equipment،
            // حتی زیر Overlap واقعی و اجباری ====
            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var activeCount = await verifyCtx.EquipmentCriticalities.CountAsync(x => x.EquipmentId == equipmentId && x.IsActive);
            Assert.Equal(1, activeCount);
        }
        finally
        {
            // ==== Cleanup ====
            await using var cleanupCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var toDelete = await cleanupCtx.EquipmentCriticalities.Where(x => x.EquipmentId == equipmentId).ToListAsync();
            cleanupCtx.EquipmentCriticalities.RemoveRange(toDelete);
            var equipment = await cleanupCtx.Equipments.FindAsync(equipmentId);
            if (equipment != null) cleanupCtx.Equipments.Remove(equipment);
            var project = await cleanupCtx.Projects.FindAsync(projectId);
            if (project != null) cleanupCtx.Projects.Remove(project);
            var discipline = await cleanupCtx.Disciplines.FindAsync(disciplineId);
            if (discipline != null) cleanupCtx.Disciplines.Remove(discipline);
            await cleanupCtx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Assess_MultipleInactiveHistoricalRecords_RemainAllowed_OnlyOneActiveEnforced()
    {
        int equipmentId, projectId, disciplineId;

        await using (var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var project = new Project { ProjectName = "EqCrit Historical Project", ProjectCode = $"HT-{Guid.NewGuid():N}"[..10], IsActive = true };
            var discipline = new Discipline { Name = "EqCrit Historical Discipline", Code = $"HD{Guid.NewGuid():N}"[..6], IsActive = true };
            seedCtx.Projects.Add(project);
            seedCtx.Disciplines.Add(discipline);
            await seedCtx.SaveChangesAsync();

            var equipment = new Equipment
            {
                TagNumber = $"EQ-{Guid.NewGuid():N}"[..12],
                Name = "Historical Test Equipment",
                EquipmentType = "Pump",
                ProjectId = project.ProjectId,
                DisciplineId = discipline.DisciplineId,
                IsActive = true
            };
            seedCtx.Equipments.Add(equipment);
            await seedCtx.SaveChangesAsync();

            equipmentId = equipment.EquipmentId;
            projectId = project.ProjectId;
            disciplineId = discipline.DisciplineId;

            // دو رکورد تاریخی غیرفعال (A و B) - باید مجاز باشند چون ایندکس فقط روی IsActive=1 فیلتر شده
            seedCtx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentId = equipmentId, CriticalityClass = "B", IsActive = false, CreatedDate = DateTime.Now.AddDays(-2) });
            seedCtx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentId = equipmentId, CriticalityClass = "C", IsActive = false, CreatedDate = DateTime.Now.AddDays(-1) });
            await seedCtx.SaveChangesAsync();
        }

        try
        {
            // یک رکورد فعال (C) - باید مجاز باشد
            await using (var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
            {
                var controller = CreateController(ctx);
                var result = await controller.Assess(equipmentId, "A", null, null, null, null, null);
                Assert.IsType<RedirectToActionResult>(result);
                Assert.Null(controller.TempData["Error"]);
            }

            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var total = await verifyCtx.EquipmentCriticalities.CountAsync(x => x.EquipmentId == equipmentId);
            var inactive = await verifyCtx.EquipmentCriticalities.CountAsync(x => x.EquipmentId == equipmentId && !x.IsActive);
            var active = await verifyCtx.EquipmentCriticalities.CountAsync(x => x.EquipmentId == equipmentId && x.IsActive);

            Assert.Equal(3, total);
            Assert.Equal(2, inactive); // دو رکورد تاریخی قبلی + رکورد جدید که با Assess غیرفعال شد؟ نه - این دو رکورد از قبل غیرفعال بودند
            Assert.Equal(1, active);
        }
        finally
        {
            await using var cleanupCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var toDelete = await cleanupCtx.EquipmentCriticalities.Where(x => x.EquipmentId == equipmentId).ToListAsync();
            cleanupCtx.EquipmentCriticalities.RemoveRange(toDelete);
            var equipment = await cleanupCtx.Equipments.FindAsync(equipmentId);
            if (equipment != null) cleanupCtx.Equipments.Remove(equipment);
            var project = await cleanupCtx.Projects.FindAsync(projectId);
            if (project != null) cleanupCtx.Projects.Remove(project);
            var discipline = await cleanupCtx.Disciplines.FindAsync(disciplineId);
            if (discipline != null) cleanupCtx.Disciplines.Remove(discipline);
            await cleanupCtx.SaveChangesAsync();
        }
    }
}
