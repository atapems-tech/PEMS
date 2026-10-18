using System.Diagnostics;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی «PEMS Import Center — Global 500-Row Batch Limitation Remediation»: قبلاً
/// MrsImportService.GetBatchDetailAsync یک Take(500) ثابت و بی‌صدا داشت - نه فقط صفحه‌ی بازبینی را
/// محدود می‌کرد، بلکه چون Commit فقط از روی Checkbox های همون صفحه‌ی رندرشده کار می‌کرد، ردیف‌های
/// بعد از ۵۰۰ در کل Batch برای همیشه غیرقابل‌مشاهده و غیرقابل‌Commit/Reject می‌ماندند (نه فقط یک
/// محدودیت UX - یک تله‌ی از‌دست‌رفتن داده). این تست‌ها ثابت می‌کنند فایل‌های بزرگ‌تر از ۵۰۰ ردیف
/// به‌عنوان یک عملیات منطقی واحد (Stage یک‌بار → Commit سراسر-Batch یک‌بار) کار می‌کنند، بدون
/// نیاز کاربر به تقسیم دستی. InMemory کافی است (همون الگوی MrsImportMetadataTests) چون این
/// آزمون‌ها حجم/صفحه‌بندی/کامل‌بودن Commit را بررسی می‌کنند، نه رفتار Concurrency سطح-SQL که در
/// MrsImportCommitConcurrencyTests (روی SQL Server واقعی) پوشش داده شده.
/// </summary>
public class MrsImportBatchCapacityTests
{
    private static (MrsImportService svc, PEMSDbContext ctx) Seed(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        return (new MrsImportService(ctx, new VendorService(ctx)), ctx);
    }

    /// <summary>یک فایل MRS واقعی با n ردیف "New" (شرح یکتا برای هرکدام، وگرنه داخل همون Batch DuplicateInBatch می‌شوند).</summary>
    private static MemoryStream BuildLargeWorkbook(int n, string tag)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "MRS No"; ws.Cell(1, 2).Value = "Item Code"; ws.Cell(1, 3).Value = "Description"; ws.Cell(1, 4).Value = "Unit"; ws.Cell(1, 5).Value = "Qty Received";
        for (var i = 1; i <= n; i++)
        {
            ws.Cell(i + 1, 1).Value = $"WH-A-{tag}-{i}";
            ws.Cell(i + 1, 2).Value = $"{tag}-CODE-{i}";
            ws.Cell(i + 1, 3).Value = $"Capacity Test Material {tag} {i}";
            ws.Cell(i + 1, 4).Value = "EA";
            ws.Cell(i + 1, 5).Value = "1";
        }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static async Task<int> StageAsync(MrsImportService svc, int n, string tag)
    {
        using var wb = BuildLargeWorkbook(n, tag);
        var (success, error, batchId) = await svc.StageFromExcelAsync(wb, $"{tag}.xlsx", null, null);
        Assert.True(success, error);
        return batchId!.Value;
    }

    // ---------------- Tests 1–4: exact boundary sizes (the reported defect was precisely at the 500/501 edge) ----------------
    [Theory]
    [InlineData(499)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task Full_file_of_n_rows_stages_completely_and_commits_completely_in_one_logical_operation(int n)
    {
        var (svc, ctx) = Seed(nameof(Full_file_of_n_rows_stages_completely_and_commits_completely_in_one_logical_operation) + n);
        var batchId = await StageAsync(svc, n, "T" + n);

        // همه‌ی ردیف‌ها Stage شدند - نه فقط ۵۰۰ تای اول
        Assert.Equal(n, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId));
        var batches = await svc.GetBatchesAsync();
        Assert.Equal(n, batches.Single(x => x.MrsImportBatchId == batchId).TotalRows);

        // ردیف‌های بعد از موقعیت ۵۰۰ (نقطه‌ی دقیق باگ قبلی) از طریق صفحه‌بندی واقعی قابل‌دسترسی‌اند
        if (n > 500)
        {
            var lastPage = (n - 1) / DataGridPageSize.Default + 1;
            var detail = await svc.GetBatchDetailAsync(batchId, "New", lastPage, DataGridPageSize.Default);
            Assert.NotNull(detail);
            Assert.NotEmpty(detail!.Rows.Items);
            Assert.Equal(n, detail.Rows.TotalCount);
        }

        // یک اقدام Commit واحد (نه n/500 اقدام دستی) کل Batch را پردازش می‌کند
        var (success, error, committed, skipped) = await svc.CommitByStatusAsync(batchId, "New", null);
        Assert.True(success, error);
        Assert.Equal(n, committed);
        Assert.Equal(0, skipped);
        // توجه: PEMSDbContext.SaveChangesAsync همیشه CreatedBy را با CurrentUserName جاری بازنویسی
        // می‌کند (فارغ از مقدار "MrsImport" که سرویس تنظیم می‌کند) - رفتاری از پیش موجود، نه نتیجه‌ی
        // این تغییرات. پس شناسایی رکوردها فقط از طریق پیشوند یکتای MaterialCode انجام می‌شود.
        Assert.Equal(n, await ctx.MaterialItems.CountAsync(x => x.MaterialCode.StartsWith("T" + n + "-CODE-")));
        Assert.Equal(0, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending"));
    }

    // ---------------- Test 5: larger file where practical, with real timing evidence ----------------
    [Fact]
    public async Task Five_thousand_row_file_stages_and_commits_as_one_operation_within_a_practical_time()
    {
        var (svc, ctx) = Seed(nameof(Five_thousand_row_file_stages_and_commits_as_one_operation_within_a_practical_time));

        var sw = Stopwatch.StartNew();
        var batchId = await StageAsync(svc, 5000, "T5000");
        var stageMs = sw.ElapsedMilliseconds;

        Assert.Equal(5000, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId));

        sw.Restart();
        var (success, error, committed, skipped) = await svc.CommitByStatusAsync(batchId, "New", null);
        var commitMs = sw.ElapsedMilliseconds;

        Assert.True(success, error);
        Assert.Equal(5000, committed);
        Assert.Equal(5000, await ctx.MaterialItems.CountAsync(x => x.MaterialCode.StartsWith("T5000-CODE-")));

        // شواهد واقعی کارایی (نه فرض) - طبق بند ۱۱ ممیزی؛ InMemory Provider است، پس این کف کارایی
        // واقعی SQL Server را نشان نمی‌دهد، فقط ثابت می‌کند حجم بزرگ کرش/timeout منطقی نمی‌کند.
        Console.WriteLine($"MRS capacity benchmark (InMemory): stage 5000 rows = {stageMs} ms, commit 5000 rows = {commitMs} ms");
        Assert.True(stageMs < 30000, $"Stage took {stageMs}ms - unexpectedly slow even for InMemory");
        Assert.True(commitMs < 60000, $"Commit took {commitMs}ms - unexpectedly slow even for InMemory");
    }

    // ---------------- Test 6: one logical Import Batch regardless of size (no silent splitting) ----------------
    [Fact]
    public async Task Large_file_remains_exactly_one_import_batch_processed_by_exactly_one_commit_call()
    {
        var (svc, ctx) = Seed(nameof(Large_file_remains_exactly_one_import_batch_processed_by_exactly_one_commit_call));
        var batchId = await StageAsync(svc, 1500, "T1500");

        Assert.Equal(1, await ctx.MrsImportBatches.CountAsync());
        Assert.Equal(1, await ctx.MrsImportBatches.CountAsync(x => x.MrsImportBatchId == batchId));

        var (success, error, committed, _) = await svc.CommitByStatusAsync(batchId, "New", null);
        Assert.True(success, error);
        Assert.Equal(1500, committed);
        Assert.Equal("Committed", (await ctx.MrsImportBatches.SingleAsync()).Status);
    }

    // ---------------- Test 7: duplicate commit does not duplicate records ----------------
    [Fact]
    public async Task Committing_the_same_status_filter_twice_does_not_create_duplicate_material_items()
    {
        var (svc, ctx) = Seed(nameof(Committing_the_same_status_filter_twice_does_not_create_duplicate_material_items));
        var batchId = await StageAsync(svc, 600, "T600");

        var first = await svc.CommitByStatusAsync(batchId, "New", null);
        Assert.True(first.Success, first.Error);
        Assert.Equal(600, first.Committed);

        var second = await svc.CommitByStatusAsync(batchId, "New", null);
        Assert.True(second.Success, second.Error);
        Assert.Equal(0, second.Committed);   // دیگر ردیف Pending ای با این فیلتر نمانده

        Assert.Equal(600, await ctx.MaterialItems.CountAsync(x => x.MaterialCode.StartsWith("T600-CODE-")));
    }

    // ---------------- Test 8: partial row validation errors are reported correctly ----------------
    [Fact]
    public async Task Batch_with_mixed_valid_and_invalid_rows_reports_each_correctly_and_never_commits_invalid_rows()
    {
        var (svc, ctx) = Seed(nameof(Batch_with_mixed_valid_and_invalid_rows_reports_each_correctly_and_never_commits_invalid_rows));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "MRS No"; ws.Cell(1, 2).Value = "Description";
        for (var i = 1; i <= 520; i++)
        {
            ws.Cell(i + 1, 1).Value = $"WH-A-MIX-{i}";
            ws.Cell(i + 1, 2).Value = i % 10 == 0 ? "" : $"Mixed Material {i}";   // هر ردیف دهم: شرح خالی => Invalid
        }
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;

        var (success, error, batchId) = await svc.StageFromExcelAsync(ms, "mixed.xlsx", null, null);
        Assert.True(success, error);

        var invalidCount = await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.MatchStatus == "Invalid");
        var newCount = await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.MatchStatus == "New");
        Assert.Equal(52, invalidCount);
        Assert.Equal(468, newCount);
        Assert.Equal(520, invalidCount + newCount);

        // Invalid قابل Commit نیست (سرویس صریح رد می‌کند - نه رد کردن خاموش)
        var invalidAttempt = await svc.CommitByStatusAsync(batchId!.Value, "Invalid", null);
        Assert.False(invalidAttempt.Success);
        Assert.Contains("Invalid", invalidAttempt.Error);

        var (cSuccess, cError, committed, skipped) = await svc.CommitByStatusAsync(batchId.Value, "New", null);
        Assert.True(cSuccess, cError);
        Assert.Equal(468, committed);
        Assert.Equal(0, skipped);
        Assert.Equal(0, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.MatchStatus == "Invalid" && x.ReviewStatus == "Approved"));
    }

    // ---------------- Test 9: a processing failure is never silently reported as full success ----------------
    [Fact]
    public async Task An_unknown_or_unsupported_status_filter_fails_explicitly_instead_of_silently_reporting_success()
    {
        var (svc, ctx) = Seed(nameof(An_unknown_or_unsupported_status_filter_fails_explicitly_instead_of_silently_reporting_success));
        var batchId = await StageAsync(svc, 10, "T10");

        var (success, error, committed, skipped) = await svc.CommitByStatusAsync(batchId, "NotARealStatus", null);
        Assert.False(success);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Equal(0, committed);
        Assert.Equal(0, skipped);
        // Batch نباید به‌اشتباه Committed علامت بخورد وقتی هیچ ردیفی واقعاً پردازش نشده
        Assert.Equal("Staged", (await ctx.MrsImportBatches.SingleAsync(x => x.MrsImportBatchId == batchId)).Status);
    }

    // ---------------- Test 10: existing small/normal-file behavior is unchanged ----------------
    // پوشش داده‌شده توسط MrsImportMetadataTests (بدون تغییر، ۳۶/۳۶ سبز پس از این رفع باگ) و
    // MrsImportCommitConcurrencyTests (بدون تغییر، سبز روی SQL Server واقعی) - همون تست‌ها،
    // بدون هیچ تغییری در این ممیزی، دوباره اجرا و تایید شدند.

    // ---------------- Additional: manual per-page selection still works exactly as before (unchanged path) ----------------
    [Fact]
    public async Task Explicit_row_id_commit_still_works_unchanged_alongside_the_new_whole_batch_action()
    {
        var (svc, ctx) = Seed(nameof(Explicit_row_id_commit_still_works_unchanged_alongside_the_new_whole_batch_action));
        var batchId = await StageAsync(svc, 600, "T600B");

        var firstPageIds = await ctx.MrsImportRows.Where(x => x.MrsImportBatchId == batchId).OrderBy(x => x.MrsImportRowId).Take(50).Select(x => x.MrsImportRowId).ToListAsync();
        var (success, error, committed, skipped) = await svc.CommitAsync(batchId, firstPageIds, null);
        Assert.True(success, error);
        Assert.Equal(50, committed);

        // بقیه‌ی ۵۵۰ ردیف هنوز Pending اند و از طریق Commit سراسر-Batch هم قابل‌دسترسی‌اند (رفتار ادغام‌شده)
        Assert.Equal(550, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending"));
        var (s2, e2, committed2, _) = await svc.CommitByStatusAsync(batchId, "New", null);
        Assert.True(s2, e2);
        Assert.Equal(550, committed2);
        Assert.Equal(0, await ctx.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending"));
    }
}
