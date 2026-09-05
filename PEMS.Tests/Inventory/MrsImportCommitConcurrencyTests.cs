using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// باگ واقعی production: SqlException روی IX_MaterialItems_MaterialCode («Cannot insert
/// duplicate key row... duplicate key value is (700009003)») در MrsImportService.CommitAsync.
/// علت اصلی: usedMaterialCodes فقط یک اسنپ‌شات در آغاز هر Commit است - وقتی دو Commit هم‌زمان
/// (دو کلیک/دو تب کاربر روی BulkCommit) هر دو ردیفی با همان RawItemCode می‌سازند، هیچ‌کدام از
/// اسنپ‌شات دیگری خبر ندارد و هر دو تلاش می‌کنند همان MaterialCode را درج کنند. این تست دقیقاً
/// همون الگوی TransmittalNumberingConcurrencyTests را روی یک دیتابیس SQL Server ایزوله (نه
/// PEMSDB مشترک) اجرا می‌کند تا ثابت کند رفع‌اشکال (catch روی Unique Constraint + تولید کد
/// جایگزین امن و تلاش دوباره) واقعاً جلوی کرش را می‌گیرد، نه فقط این‌که تئوری درست به‌نظر برسد.
/// </summary>
public class MrsImportCommitConcurrencyTests
{
    private const string IsolatedDbName = "PEMSDB_MrsCommitConcurrencyTest";

    [Fact]
    public async Task CommitAsync_TwoConcurrentCommitsSameRawItemCode_BothSucceedNoDuplicateMaterialCode()
    {
        var sharedItemCode = $"{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        int batchId, row1Id, row2Id;

        // ==== Seed: یک Batch با دو ردیف Pending/New، هر دو با همان RawItemCode ولی شرح متفاوت
        // (پس نه در Stage با seenNormalized و نه در Commit با createdInThisCommit به‌هم برخورد
        // نمی‌کنند - تنها راه تشخیص تداخل، خودِ MaterialCode است) ====
        await using (var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var batch = new MrsImportBatch
            {
                SourceFileName = "concurrency-test.xlsx",
                TotalRows = 2,
                Status = "InReview"
            };
            var row1 = new MrsImportRow
            {
                Batch = batch,
                RawItemCode = sharedItemCode,
                RawDescription = $"Item A {Guid.NewGuid():N}",
                NormalizedDescription = $"item a {Guid.NewGuid():N}".ToLowerInvariant(),
                MatchStatus = "New",
                ReviewStatus = "Pending"
            };
            var row2 = new MrsImportRow
            {
                Batch = batch,
                RawItemCode = sharedItemCode,
                RawDescription = $"Item B {Guid.NewGuid():N}",
                NormalizedDescription = $"item b {Guid.NewGuid():N}".ToLowerInvariant(),
                MatchStatus = "New",
                ReviewStatus = "Pending"
            };

            seedCtx.MrsImportBatches.Add(batch);
            seedCtx.MrsImportRows.AddRange(row1, row2);
            await seedCtx.SaveChangesAsync();

            batchId = batch.MrsImportBatchId;
            row1Id = row1.MrsImportRowId;
            row2Id = row2.MrsImportRowId;
        }

        // ==== Race: دو Commit هم‌زمان، هر کدام فقط یکی از دو ردیف را می‌گیرد، هر کدام
        // DbContext/Service کاملاً مستقل - دقیقاً معادل دو درخواست HTTP هم‌زمان BulkCommit ====
        var task1 = Task.Run(async () =>
        {
            await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var svc = new MrsImportService(ctx, new VendorService(ctx));
            return await svc.CommitAsync(batchId, new List<int> { row1Id }, null);
        });
        var task2 = Task.Run(async () =>
        {
            await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName);
            var svc = new MrsImportService(ctx, new VendorService(ctx));
            return await svc.CommitAsync(batchId, new List<int> { row2Id }, null);
        });

        var results = await Task.WhenAll(task1, task2);

        // ==== Assertion 1: هیچ‌کدام نباید با Exception خام کرش کند - هر دو باید موفق برگردند ====
        Assert.All(results, r => Assert.True(r.Success, r.Error));
        Assert.All(results, r => Assert.Equal(1, r.Committed));

        // ==== Assertion 2: دقیقاً دو MaterialItem واقعاً ساخته شده، با MaterialCode متفاوت
        // (یکی می‌تواند sharedItemCode خام باشد، دیگری باید به کد جایگزین امن افتاده باشد) ====
        await using (var verifyCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var rows = await verifyCtx.MrsImportRows
                .Where(x => x.MrsImportBatchId == batchId)
                .ToListAsync();

            Assert.All(rows, r => Assert.NotNull(r.ResultMaterialItemId));
            var materialItemIds = rows.Select(r => r.ResultMaterialItemId!.Value).Distinct().ToList();
            Assert.Equal(2, materialItemIds.Count);

            var materialCodes = await verifyCtx.MaterialItems
                .Where(x => materialItemIds.Contains(x.MaterialItemId))
                .Select(x => x.MaterialCode)
                .ToListAsync();

            Assert.Equal(2, materialCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        // ==== Cleanup: پاک‌سازی داده‌ی این اجرا تا دیتابیس ایزوله برای اجرای بعدی تمیز بماند ====
        await using (var cleanupCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow(IsolatedDbName))
        {
            var rows = await cleanupCtx.MrsImportRows.Where(x => x.MrsImportBatchId == batchId).ToListAsync();
            var materialItemIds = rows.Where(r => r.ResultMaterialItemId.HasValue)
                .Select(r => r.ResultMaterialItemId!.Value).Distinct().ToList();

            cleanupCtx.MrsImportRows.RemoveRange(rows);
            var batch = await cleanupCtx.MrsImportBatches.FindAsync(batchId);
            if (batch != null) cleanupCtx.MrsImportBatches.Remove(batch);

            var materials = await cleanupCtx.MaterialItems.Where(x => materialItemIds.Contains(x.MaterialItemId)).ToListAsync();
            cleanupCtx.MaterialItems.RemoveRange(materials);

            await cleanupCtx.SaveChangesAsync();

            Assert.Equal(0, await cleanupCtx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId));
        }
    }
}
