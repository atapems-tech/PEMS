using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P4 closure (Workstream C) - علت رشد درجه‌ی دو Commit فایل‌های بزرگ MRS: Attach(row) کل گراف Batch (Batch.Rows + Metadata) را در هر
/// بلوک ۱۰۰تایی دوباره ردیابی می‌کرد. این تست زمان‌محور نیست: حداکثر تعداد Entity ردیابی‌شده در لحظه‌ی SaveChanges باید به اندازه‌ی یک بلوک بماند،
/// نه اندازه‌ی کل Batch.
/// </summary>
public class MrsImportCommitTrackingTests
{
    private sealed class MaxTracked : SaveChangesInterceptor
    {
        public int Max;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
        {
            Max = Math.Max(Max, e.Context!.ChangeTracker.Entries().Count());
            return base.SavingChangesAsync(e, r, ct);
        }
    }

    private static MemoryStream Workbook(int n)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "MRS No"; ws.Cell(1, 2).Value = "Item Code"; ws.Cell(1, 3).Value = "Description"; ws.Cell(1, 4).Value = "Unit"; ws.Cell(1, 5).Value = "Qty Received";
        for (var i = 1; i <= n; i++)
        {
            ws.Cell(i + 1, 1).Value = $"WH-A-TRK-{i}"; ws.Cell(i + 1, 2).Value = $"TRK-CODE-{i}";
            ws.Cell(i + 1, 3).Value = $"Tracking Material {i}"; ws.Cell(i + 1, 4).Value = "EA"; ws.Cell(i + 1, 5).Value = "1";
        }
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0; return ms;
    }

    [Theory]
    [InlineData(false)]   // همان Context مرحله‌ی Stage (سناریوی تست‌ها)
    [InlineData(true)]    // Context تازه (سناریوی واقعی: درخواست جداگانه‌ی Commit)
    public async Task Commit_KeepsTheTrackedEntityCountBoundedByTheChunk_NotTheWholeBatch(bool freshContext)
    {
        var name = "trk_" + Guid.NewGuid().ToString("N");
        var tracker = new MaxTracked();
        DbContextOptions<PEMSDbContext> Options() => new DbContextOptionsBuilder<PEMSDbContext>().UseInMemoryDatabase(name).AddInterceptors(tracker).Options;

        await using var stageCtx = new PEMSDbContext(Options());
        var stageSvc = new MrsImportService(stageCtx, new VendorService(stageCtx));
        using var wb = Workbook(1200);
        var (ok, err, batchId) = await stageSvc.StageFromExcelAsync(wb, "t.xlsx", null, null);
        Assert.True(ok, err);

        tracker.Max = 0;                                               // فقط خود Commit سنجیده می‌شود
        await using var commitCtx = freshContext ? new PEMSDbContext(Options()) : stageCtx;
        var svc = freshContext ? new MrsImportService(commitCtx, new VendorService(commitCtx)) : stageSvc;

        var (success, error, committed, skipped) = await svc.CommitByStatusAsync(batchId!.Value, "New", null);

        Assert.True(success, error);
        Assert.Equal(1200, committed);
        Assert.Equal(0, skipped);
        Assert.True(tracker.Max < 1000, $"tracked entities at SaveChanges peaked at {tracker.Max} (batch = 1200 rows) - the whole batch graph is being re-attached per chunk");
    }
}
