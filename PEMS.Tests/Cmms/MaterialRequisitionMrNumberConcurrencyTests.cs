using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B Wave A — ممیزی مستقل نهایی» بند P1-03: بررسی AnyAsync قبل از Insert در
/// برابر Race Condition امن نیست. این تست روی یک دیتابیس SQL Server کاملاً ایزوله اجرا می‌شود
/// (نه PEMSDB مشترک Dev) - همون الگوی دقیق TransmittalNumberingConcurrencyTests - ساخته‌شده از
/// روی تاریخچه‌ی Migration واقعی پروژه، پس همون ایندکس Unique واقعی
/// (IX_MaterialRequisitions_MRNumber) که تولید هم دارد را دارد. اگر SQL Server در دسترس نباشد،
/// تست با پیام واضح Fail می‌شود، نه Skip خاموش.
/// </summary>
public class MaterialRequisitionMrNumberConcurrencyTests
{
    private const string IsolatedDbName = "PEMSDB_ConcurrencyTest";

    [Fact]
    public async Task CreateAsync_TenConcurrentRequestsSameMrNumber_ExactlyOneWinnerNoDuplicatePersisted()
    {
        var mrNumber = $"MR-CONC-{Guid.NewGuid():N}"[..20];
        int projectId, disciplineId;

        // ==== Seed: Project/Discipline واقعی در دیتابیس ایزوله (نه در PEMSDB) ====
        await using (var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var project = new Project { ProjectName = "Concurrency Test Project", ProjectCode = $"CT-{Guid.NewGuid():N}"[..10], IsActive = true };
            var discipline = new Discipline { Name = "Concurrency Test Discipline", Code = $"CD{Guid.NewGuid():N}"[..6], IsActive = true };
            seedCtx.Projects.Add(project);
            seedCtx.Disciplines.Add(discipline);
            await seedCtx.SaveChangesAsync();
            projectId = project.ProjectId;
            disciplineId = discipline.DisciplineId;
        }

        try
        {
            // ==== Race: ۱۰ درخواست هم‌زمان، همون MRNumber، هر کدام DbContext/Service مستقل ====
            const int concurrentRequests = 10;

            var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
                var svc = new MaterialRequisitionService(ctx);

                try
                {
                    var (success, error, id) = await svc.CreateAsync(
                        mrNumber, projectId, disciplineId, null, "Concurrency test MR", null, null, null, new());

                    return (Won: success, Id: id, Error: error);
                }
                catch (DbUpdateException ex)
                {
                    // طبق دستورالعمل ممیزی (بند ۱۲): CreateAsync عمداً فقط نقض یکتای واقعی
                    // (2601/2627) را می‌گیرد، نه هر DbUpdateException دیگری - مثلاً Deadlock
                    // (خطای 1205 SQL Server) که می‌تواند زیر تلاش ۱۰-تایی کاملاً هم‌زمان روی
                    // همون ایندکس Unique رخ دهد و یک پدیده‌ی شناخته‌شده و مستقل از این تغییر
                    // است (برای هر ستون Unique دیگری هم زیر همین بار رخ می‌دهد - نه چیزی که این
                    // Fix وارد کرده). این تست به‌جای قورت‌دادن آن، آن را هم به‌عنوان یک
                    // «بازنده‌ی کنترل‌شده» می‌شمارد - چون Invariant واقعی (دقیقاً یک ردیف
                    // Persist شود) مستقل از اینکه علت شکست کدام خطای SQL بوده، در Assertion زیر
                    // مستقیماً از خودِ دیتابیس بررسی می‌شود.
                    return (Won: false, Id: (int?)null, Error: ex.Message);
                }
            });

            var results = await Task.WhenAll(tasks);

            // ==== Assertion 1: حداقل یک برنده و حداقل یک بازنده ====
            var winners = results.Where(r => r.Won).ToList();
            var losers = results.Where(r => !r.Won).ToList();

            Assert.NotEmpty(winners);
            Assert.NotEmpty(losers);
            Assert.All(losers, l => Assert.False(string.IsNullOrWhiteSpace(l.Error)));

            // ==== Assertion 2 (تضمین اصلی): صحت دیتابیس - دقیقاً یک ردیف با این MRNumber
            // واقعاً Persist شده، مستقل از این‌که هر بازنده دقیقاً با چه خطایی رد شد ====
            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var persistedCount = await verifyCtx.MaterialRequisitions.CountAsync(x => x.MRNumber == mrNumber);
            Assert.Equal(1, persistedCount);
        }
        finally
        {
            // ==== Cleanup: پاک‌سازی داده‌ی این اجرای تست - حتی اگر Assertion بالا شکست بخورد ====
            await using var cleanupCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var toDelete = await cleanupCtx.MaterialRequisitions.Where(x => x.MRNumber == mrNumber).ToListAsync();
            cleanupCtx.MaterialRequisitions.RemoveRange(toDelete);

            var project = await cleanupCtx.Projects.FindAsync(projectId);
            if (project != null) cleanupCtx.Projects.Remove(project);
            var discipline = await cleanupCtx.Disciplines.FindAsync(disciplineId);
            if (discipline != null) cleanupCtx.Disciplines.Remove(discipline);

            await cleanupCtx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_MrNumberWithSurroundingWhitespace_TreatedAsSameCanonicalNumber()
    {
        var baseNumber = $"MR-WS-{Guid.NewGuid():N}"[..16];
        int projectId, disciplineId;

        await using (var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var project = new Project { ProjectName = "Whitespace Test Project", ProjectCode = $"WT-{Guid.NewGuid():N}"[..10], IsActive = true };
            var discipline = new Discipline { Name = "Whitespace Test Discipline", Code = $"WD{Guid.NewGuid():N}"[..6], IsActive = true };
            seedCtx.Projects.Add(project);
            seedCtx.Disciplines.Add(discipline);
            await seedCtx.SaveChangesAsync();
            projectId = project.ProjectId;
            disciplineId = discipline.DisciplineId;
        }

        try
        {
            await using var ctx1 = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var svc1 = new MaterialRequisitionService(ctx1);
            var first = await svc1.CreateAsync(baseNumber, projectId, disciplineId, null, "First", null, null, null, new());

            await using var ctx2 = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var svc2 = new MaterialRequisitionService(ctx2);
            var second = await svc2.CreateAsync($"  {baseNumber}  ", projectId, disciplineId, null, "Second", null, null, null, new());

            Assert.True(first.Success);
            Assert.False(second.Success);
            Assert.NotNull(second.Error);

            await using var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            Assert.Equal(1, await verifyCtx.MaterialRequisitions.CountAsync(x => x.MRNumber == baseNumber));
        }
        finally
        {
            await using var cleanupCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var toDelete = await cleanupCtx.MaterialRequisitions.Where(x => x.MRNumber == baseNumber).ToListAsync();
            cleanupCtx.MaterialRequisitions.RemoveRange(toDelete);
            var project = await cleanupCtx.Projects.FindAsync(projectId);
            if (project != null) cleanupCtx.Projects.Remove(project);
            var discipline = await cleanupCtx.Disciplines.FindAsync(disciplineId);
            if (discipline != null) cleanupCtx.Disciplines.Remove(discipline);
            await cleanupCtx.SaveChangesAsync();
        }
    }
}
