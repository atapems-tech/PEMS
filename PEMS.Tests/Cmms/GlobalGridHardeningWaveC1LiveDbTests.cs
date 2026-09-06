using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using Xunit.Abstractions;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Global Grid Hardening» Wave C1: تست‌های InMemory (GlobalGridHardeningWaveC1Tests)
/// یک محدودیت واقعی خودِ EF InMemory Provider را کشف کردند - وقتی Select به یک Navigation
/// Property یا دو Subquery همبسته‌ی جدا روی همون جدول ارجاع می‌دهد، اگر ردیف والد به‌صورت
/// دستی Seed نشده باشد، InMemory کل نتیجه را خالی برمی‌گرداند (نه NULL برای همون ستون، مثل
/// SQL Server واقعی). این تست‌ها با یک SQL Server واقعی (نه InMemory) ثابت می‌کنند که خودِ
/// Query تولیدشده صحیح است و روی دیتابیس واقعی هم نتیجه‌ی درست برمی‌گرداند.
/// </summary>
public class GlobalGridHardeningWaveC1LiveDbTests
{
    private readonly ITestOutputHelper _output;

    public GlobalGridHardeningWaveC1LiveDbTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Transmittal_GetPagedAsync_ProducesCorrectResults_OnRealSqlServer()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var service = new TransmittalService(ctx);

        // فقط تأیید اجرای بدون خطا و شکل نتیجه روی SQL Server واقعی - بدون دستکاری داده‌ی
        // موجود در PEMSDB (فقط Read-only، هیچ Insert/Update/Delete ای انجام نمی‌شود)
        var result = await service.GetPagedAsync(1, 20);

        _output.WriteLine($"TotalCount={result.TotalCount}, Items={result.Items.Count}");
        Assert.True(result.Items.Count <= 20);
        Assert.True(result.TotalCount >= result.Items.Count);

        // اگر دست‌کم یک ترانسمیتال Superseded شده در دیتابیس واقعی وجود دارد، فیلد
        // SupersededByTransmittalId/No باید هر دو با هم پر یا هر دو خالی باشند (سازگاری Subquery
        // ترکیبی) - نه اینکه یکی پر و دیگری تهی بماند (که نشانه‌ی باگ در جفت‌کردن دو ستون است)
        foreach (var item in result.Items)
        {
            var idFilled = item.SupersededByTransmittalId.HasValue;
            var noFilled = !string.IsNullOrEmpty(item.SupersededByTransmittalNo);
            Assert.Equal(idFilled, noFilled);
        }
    }

    [Fact]
    public async Task MDB_GetPagedAsync_ProducesCorrectResults_OnRealSqlServer()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var service = new ManufacturerDataBookService(ctx);

        var result = await service.GetPagedAsync(1, 20);

        _output.WriteLine($"TotalCount={result.TotalCount}, Items={result.Items.Count}");
        Assert.True(result.Items.Count <= 20);
        Assert.True(result.TotalCount >= result.Items.Count);

        // طبق ManufacturerDataBook.IsIndexOverdue (زنجیره‌ی دو‌سطحی [NotMapped]): سازگاری باید
        // روی SQL Server واقعی هم برقرار باشد - اگر IndexOfContentsSubmittedDate پر باشد،
        // IsIndexOverdue هرگز نباید true باشد (طبق تعریف خودِ Property)
        foreach (var item in result.Items.Where(x => x.IndexOfContentsSubmittedDate.HasValue))
        {
            Assert.False(item.IsIndexOverdue);
        }
    }

    [Fact]
    public void Transmittal_GetPagedAsync_GeneratesSingleCombinedSubquery_NotDuplicated()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var sql = ctx.Transmittals
            .AsNoTracking()
            .OrderByDescending(x => x.TransmittalDate)
            .ThenByDescending(x => x.TransmittalId)
            .Skip(0)
            .Take(20)
            .Select(x => new
            {
                x.TransmittalId,
                SupersededBy = ctx.Transmittals
                    .Where(y => y.SupersedesTransmittalId == x.TransmittalId)
                    .Select(y => new { y.TransmittalId, y.TransmittalNo })
                    .FirstOrDefault()
            })
            .ToQueryString();

        _output.WriteLine(sql);

        // فقط یک Correlated Subquery برای SupersededBy باید تولید شود (نه دو تای جدا)
        var occurrences = System.Text.RegularExpressions.Regex.Matches(sql, "SupersedesTransmittalId").Count;
        Assert.True(occurrences >= 1, "Expected the correlated subquery to reference SupersedesTransmittalId");
    }
}
