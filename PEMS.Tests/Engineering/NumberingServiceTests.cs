using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-01 (رفع P0 ممیزی شماره‌گذاری): این تست‌ها به یک SQL Server واقعی نیاز دارند چون
/// NumberingService.IncrementAsync از T-SQL خام "UPDATE ... OUTPUT" استفاده می‌کند - نه InMemory
/// و نه SQLite این را پشتیبانی می‌کنند. اگر دیتابیس در دسترس نباشد، تست با پیام واضح Fail
/// می‌شود (نه Skip خاموش) - دلیل در PemsDbContextFactory.CreateSqlServerOrThrow مستند است.
/// هر تست از یک ScopeKey تصادفی (GUID) استفاده می‌کند تا با داده‌ی واقعی/سایر اجراهای تست
/// تداخل نکند و نیازی به Cleanup نداشته باشد.
/// </summary>
public class NumberingServiceTests
{
    private static string FreshScope() => $"test-{Guid.NewGuid():N}";

    [Fact]
    public async Task GetNextNumberAsync_FirstCall_ReturnsSequenceOne()
    {
        await using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var service = new NumberingService(ctx);

        var number = await service.GetNextNumberAsync("EngineeringDocument", FreshScope());

        Assert.EndsWith("0001", number);
    }

    [Fact]
    public async Task GetNextNumberAsync_SequentialCalls_IncrementWithoutGaps()
    {
        await using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var service = new NumberingService(ctx);
        var scope = FreshScope();

        var first = await service.GetNextNumberAsync("EngineeringDocument", scope);
        var second = await service.GetNextNumberAsync("EngineeringDocument", scope);
        var third = await service.GetNextNumberAsync("EngineeringDocument", scope);

        Assert.EndsWith("0001", first);
        Assert.EndsWith("0002", second);
        Assert.EndsWith("0003", third);
    }

    [Fact]
    public async Task GetNextNumberAsync_ConcurrentCalls_NeverProduceDuplicates()
    {
        // طبق درخواست ممیزی: این دقیقاً همان سناریوی باگ اصلی است - دو (اینجا ۱۰) کاربر هم‌زمان
        // برای همون Project+Discipline+DocumentType شماره می‌گیرند.
        var scope = FreshScope();
        const int concurrentRequests = 10;

        // هر Task باید DbContext/NumberingService مستقل خودش را داشته باشد - DbContext Thread-Safe
        // نیست و نباید بین Task های موازی به اشتراک گذاشته شود.
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            await using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
            var service = new NumberingService(ctx);
            return await service.GetNextNumberAsync("EngineeringDocument", scope);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrentRequests, results.Distinct().Count());
    }
}
