using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی Material Management (Gap A) + Step 2 بخش ۱۲: شماره‌گذاری IRN باید از همون
/// INumberingService موجود (UPDATE...OUTPUT اتمیک) استفاده کند - نه Count()+1/MAX()+1. این
/// تست‌ها روی SQL Server واقعی اجرا می‌شوند چون NumberingService از SqlQueryRaw استفاده
/// می‌کند که EF InMemory Provider اصلاً پشتیبانی نمی‌کند.
/// </summary>
public class IrnNumberingRealSqlTests
{
    [Fact]
    public async Task GetNextNumberAsync_for_IRN_produces_unique_sequential_numbers()
    {
        await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMS_TEST_IRN_NUMBERING");
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
        var svc = new NumberingService(ctx);

        var first = await svc.GetNextNumberAsync("IRN");
        var second = await svc.GetNextNumberAsync("IRN");
        var third = await svc.GetNextNumberAsync("IRN");

        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.Matches(@"^IRN-\d{4}-\d{4}$", first);

        // ترتیبی - نه تصادفی/GUID.
        var firstSerial = int.Parse(first.Split('-')[2]);
        var secondSerial = int.Parse(second.Split('-')[2]);
        var thirdSerial = int.Parse(third.Split('-')[2]);
        Assert.Equal(firstSerial + 1, secondSerial);
        Assert.Equal(secondSerial + 1, thirdSerial);
    }

    [Fact]
    public async Task Concurrent_IRN_number_generation_never_produces_a_duplicate()
    {
        await using var seedCtx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMS_TEST_IRN_NUMBERING_CONCURRENT");
        await seedCtx.Database.EnsureDeletedAsync();
        await seedCtx.Database.MigrateAsync();

        const int concurrency = 15;
        var tasks = new Task<string>[concurrency];

        for (var i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                await using var ctx = PemsDbContextFactory.CreateIsolatedSqlServerOrThrow("PEMS_TEST_IRN_NUMBERING_CONCURRENT");
                var svc = new NumberingService(ctx);
                return await svc.GetNextNumberAsync("IRN");
            });
        }

        var numbers = await Task.WhenAll(tasks);

        Assert.Equal(concurrency, numbers.Distinct().Count());
    }
}
