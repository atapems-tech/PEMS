using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: FunctionalLocation Grid». این یک ساختار درختی است که با
/// Breadcrumb مرتب می‌شود - این مرتب‌سازی ذاتاً در حافظه انجام می‌شود (نه یک ستون DB)، پس
/// GetPagedListAsync باید صفحه‌بندی را روی نتیجه‌ی نهایی/کامل‌محاسبه‌شده انجام دهد، نه قبل از
/// آن (وگرنه دقیقاً همون باگ MaterialItem.lowStockOnly تکرار می‌شد).
/// </summary>
public class FunctionalLocationGridPaginationTests
{
    private static FunctionalLocationService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"FLGrid_{dbName}");
        return new FunctionalLocationService(ctx);
    }

    private static int _nextId = 1;

    private static FunctionalLocation NewFl(string code, int? parentId = null) => new()
    {
        FunctionalLocationId = _nextId++,
        Code = code,
        Name = code,
        ParentId = parentId,
        IsActive = true
    };

    [Fact]
    public async Task GetPagedListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetPagedListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.FunctionalLocations.Add(NewFl($"FL-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedListAsync(1, 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetPagedListAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetPagedListAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.FunctionalLocations.Add(NewFl($"FL-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedListAsync(1, pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetPagedListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetPagedListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.FunctionalLocations.Add(NewFl("FL-001"));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedListAsync(1, 13);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetPagedListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetPagedListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.FunctionalLocations.Add(NewFl($"FL-{i:D3}"));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetPagedListAsync(1, 20);
        var page2 = await service.GetPagedListAsync(2, 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.FunctionalLocationId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetPagedListAsync_DeterministicOrdering_SameOrderTwice()
    {
        var service = CreateService(nameof(GetPagedListAsync_DeterministicOrdering_SameOrderTwice), out var ctx);
        for (var i = 0; i < 10; i++)
            ctx.FunctionalLocations.Add(NewFl($"FL-{i:D3}"));
        await ctx.SaveChangesAsync();

        var first = await service.GetPagedListAsync(1, 20);
        var second = await service.GetPagedListAsync(1, 20);

        Assert.Equal(first.Items.Select(x => x.FunctionalLocationId), second.Items.Select(x => x.FunctionalLocationId));
    }

    [Fact]
    public async Task GetPagedListAsync_InactiveLocations_AreExcluded()
    {
        var service = CreateService(nameof(GetPagedListAsync_InactiveLocations_AreExcluded), out var ctx);
        var active = NewFl("FL-ACTIVE");
        var inactive = NewFl("FL-INACTIVE");
        inactive.IsActive = false;
        ctx.FunctionalLocations.Add(active);
        ctx.FunctionalLocations.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedListAsync(1, 20);

        Assert.Single(result.Items);
        Assert.Equal(active.FunctionalLocationId, result.Items[0].FunctionalLocationId);
    }

    [Fact]
    public async Task GetPagedListAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetPagedListAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetPagedListAsync(1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedListAsync_FiltersAcrossFullHierarchy_NotJustFirstPage()
    {
        // این تست بخش «صفحه‌بندی بعد از محاسبه‌ی کامل درخت» را ثابت می‌کند: تعداد کل باید
        // شامل کل مجموعه‌ی فعال باشد، نه فقط یک تکه‌ی از پیش صفحه‌بندی‌شده. Parent/Child هم
        // باید هر دو در نتیجه‌ی کامل حاضر باشند (Breadcrumb از byId کامل ساخته می‌شود).
        var service = CreateService(nameof(GetPagedListAsync_FiltersAcrossFullHierarchy_NotJustFirstPage), out var ctx);
        var root = NewFl("PLANT-A");
        ctx.FunctionalLocations.Add(root);
        for (var i = 0; i < 30; i++)
            ctx.FunctionalLocations.Add(NewFl($"AREA-{i:D3}", root.FunctionalLocationId));
        await ctx.SaveChangesAsync();

        var result = await service.GetPagedListAsync(2, 20);

        Assert.Equal(31, result.TotalCount);
        Assert.Equal(11, result.Items.Count);
        Assert.All(result.Items.Where(x => x.ParentId.HasValue), x => Assert.Equal("PLANT-A", x.ParentCode));
    }
}
