using System.Diagnostics;
using PEMS.Persistence.Repositories;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-05C (Performance Gate، §7): اندازه‌گیری واقعی روی حجم واقعی دیتابیس dev
/// (~9,226 EngineeringDocument، ~20,223 DocumentRevision، ~756 WorkflowInstance) - نه یک
/// تخمین. این تست همیشه Pass می‌شود (فقط اندازه می‌گیرد)؛ عدد واقعی در گزارش E-05C ثبت شده.
/// </summary>
public class EngineeringRevisionQualityPerformanceTests
{
    [Fact]
    public async Task GetRevisionQualityAsync_UnscopedAcrossFullDataset_CompletesInReasonableTime()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var repo = new EngineeringDocumentRepository(ctx);

        var sw1 = Stopwatch.StartNew();
        var results = await repo.GetRevisionQualityAsync(false, null, null);
        sw1.Stop();

        // اجرای دوم (Warm - بدون هزینه‌ی باز کردن Connection/JIT اولیه) برای عدد واقعی‌تر
        var sw2 = Stopwatch.StartNew();
        await repo.GetRevisionQualityAsync(false, null, null);
        sw2.Stop();

        File.AppendAllText(Path.Combine(Path.GetTempPath(), "e05c_perf.txt"),
            $"GetRevisionQualityAsync (unscoped, {results.Count} rows): cold={sw1.ElapsedMilliseconds}ms warm={sw2.ElapsedMilliseconds}ms\n");

        // فقط ثبت - این تست به‌عنوان Gate سخت‌گیرانه طراحی نشده (حجم دیتابیس dev می‌تواند
        // تغییر کند)؛ عدد واقعی به گزارش E-05C منتقل می‌شود.
        Assert.NotEmpty(results);
        Assert.True(sw2.ElapsedMilliseconds < 30000, $"Took {sw2.ElapsedMilliseconds}ms - investigate before shipping");
    }

    [Fact]
    public async Task GetPagedAsync_FirstPage_CompletesQuickly()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var repo = new EngineeringDocumentRepository(ctx);

        // طبق سند Global Data Grid Standard بند ۴: فقط ۲۰/۵۰/۱۰۰ اندازه‌ی صفحه‌ی مجاز است -
        // مقدار ۲۵ دیگر معتبر نیست (به ۵۰ Normalize می‌شد)؛ این‌جا صریحاً یکی از مقادیر مجاز
        // (۲۰) استفاده می‌شود
        var sw1 = Stopwatch.StartNew();
        var result = await repo.GetPagedAsync(new PEMS.Application.DTOs.EngineeringDocumentListFilter { PageSize = 20 }, false, null);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        await repo.GetPagedAsync(new PEMS.Application.DTOs.EngineeringDocumentListFilter { PageSize = 20 }, false, null);
        sw2.Stop();

        File.AppendAllText(Path.Combine(Path.GetTempPath(), "e05c_perf.txt"),
            $"GetPagedAsync (page 1, size 20, of {result.TotalCount} total): cold={sw1.ElapsedMilliseconds}ms warm={sw2.ElapsedMilliseconds}ms\n");

        Assert.True(result.Items.Count <= 20);
        Assert.True(sw2.ElapsedMilliseconds < 5000, $"Took {sw2.ElapsedMilliseconds}ms - investigate before shipping");
    }
}
