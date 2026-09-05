using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق «PEMS Phase 6B — P0: MaterialItem Correctness Bug». قبلاً lowStockOnly بعد از
/// CountAsync/Skip/Take روی همون صفحه‌ی نتیجه در حافظه اعمال می‌شد - یعنی TotalCount/
/// TotalPages روی مجموعه‌ی فیلترنشده محاسبه می‌شد و آیتم‌های کم‌موجودی صفحات بعدی هرگز دیده
/// نمی‌شدند. این تست‌ها دقیقاً سناریوهایی را پوشش می‌دهند که آن باگ را آشکار می‌کردند: فیلتر
/// در SQL (قبل از صفحه‌بندی) باید TotalCount/TotalPages/ReturnedRows درست را تولید کند.
/// </summary>
public class MaterialItemLowStockPaginationTests
{
    private static MaterialItemService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"MaterialItemLowStock_{dbName}");
        ctx.Warehouses.Add(new Warehouse { WarehouseId = 1, Code = "WH1", Name = "Main Warehouse", IsActive = true });
        ctx.SaveChanges();
        return new MaterialItemService(ctx);
    }

    private static int _nextId = 1;

    // reorderPoint=null => IsLowStock همیشه false (فرمول ReorderPoint.HasValue && Available <= ReorderPoint)
    private static (MaterialItem Item, MaterialStock? Stock) NewItem(
        string code, decimal? onHand = null, decimal? reserved = null, decimal? reorderPoint = null)
    {
        var id = _nextId++;
        var item = new MaterialItem
        {
            MaterialItemId = id,
            MaterialCode = code,
            Description = $"Item {code}",
            IsActive = true
        };

        MaterialStock? stock = null;
        if (onHand.HasValue)
        {
            stock = new MaterialStock
            {
                MaterialItemId = id,
                MaterialItem = item,
                WarehouseId = 1,
                OnHandQuantity = onHand.Value,
                ReservedQuantity = reserved ?? 0,
                ReorderPoint = reorderPoint,
                IsActive = true
            };
        }

        return (item, stock);
    }

    private static void AddAll(PEMS.Persistence.Context.PEMSDbContext ctx, params (MaterialItem Item, MaterialStock? Stock)[] rows)
    {
        foreach (var (item, stock) in rows)
        {
            ctx.MaterialItems.Add(item);
            if (stock != null) ctx.MaterialStocks.Add(stock);
        }
    }

    [Fact]
    public async Task LowStockOnly_False_ReturnsAllActiveItems_Unfiltered()
    {
        var service = CreateService(nameof(LowStockOnly_False_ReturnsAllActiveItems_Unfiltered), out var ctx);
        AddAll(ctx,
            NewItem("MI-LOW", onHand: 2, reorderPoint: 10),   // low stock
            NewItem("MI-OK", onHand: 50, reorderPoint: 10),   // not low stock
            NewItem("MI-NOSTOCK"));                            // no stock row at all
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25, lowStockOnly: false);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task LowStockOnly_True_TotalCountReflectsOnlyLowStockItems_NotFullTable()
    {
        // این دقیقاً همون سناریوی باگ اصلی گزارش فاز ۶A: ۳ آیتم کم‌موجودی از ۵ آیتم فعال
        var service = CreateService(nameof(LowStockOnly_True_TotalCountReflectsOnlyLowStockItems_NotFullTable), out var ctx);
        AddAll(ctx,
            NewItem("MI-LOW-1", onHand: 1, reorderPoint: 10),
            NewItem("MI-LOW-2", onHand: 2, reorderPoint: 10),
            NewItem("MI-LOW-3", onHand: 3, reorderPoint: 10),
            NewItem("MI-OK-1", onHand: 100, reorderPoint: 10),
            NewItem("MI-OK-2", onHand: 200, reorderPoint: 10));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25, lowStockOnly: true);

        Assert.Equal(3, result.TotalCount); // نه ۵ (کل جدول)
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, x => Assert.True(x.IsLowStock));
    }

    [Fact]
    public async Task LowStockOnly_True_WithMoreMatchesThanOnePage_SecondPageStillReturnsRemainingLowStockItems()
    {
        // بند صریح فاز ۶B: "more matching records than one page". قبل از رفع باگ، چون فیلتر
        // بعد از Skip/Take اعمال می‌شد، هیچ راهی برای دیدن آیتم‌های کم‌موجودی صفحه‌ی دوم نبود.
        var service = CreateService(nameof(LowStockOnly_True_WithMoreMatchesThanOnePage_SecondPageStillReturnsRemainingLowStockItems), out var ctx);
        for (var i = 0; i < 25; i++)
            AddAll(ctx, NewItem($"MI-LOW-{i:D3}", onHand: 1, reorderPoint: 10));
        for (var i = 0; i < 25; i++)
            AddAll(ctx, NewItem($"MI-OK-{i:D3}", onHand: 100, reorderPoint: 10));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20, lowStockOnly: true);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20, lowStockOnly: true);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count); // نه صفر - قبل از رفع باگ این می‌توانست صفر یا ناقص باشد
        Assert.All(page1.Items.Concat(page2.Items), x => Assert.True(x.IsLowStock));

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.MaterialItemId).ToList();
        Assert.Equal(25, allIds.Distinct().Count()); // بدون تکرار، بدون افتادگی بین صفحات
    }

    [Fact]
    public async Task LowStockOnly_True_ExactPageBoundary_NoOffByOne()
    {
        // دقیقاً ۲۰ آیتم کم‌موجودی با pageSize=20: صفحه‌ی اول باید کامل باشد، صفحه‌ی دوم خالی
        var service = CreateService(nameof(LowStockOnly_True_ExactPageBoundary_NoOffByOne), out var ctx);
        for (var i = 0; i < 20; i++)
            AddAll(ctx, NewItem($"MI-LOW-{i:D3}", onHand: 1, reorderPoint: 10));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20, lowStockOnly: true);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20, lowStockOnly: true);

        Assert.Equal(20, page1.TotalCount);
        Assert.Equal(1, page1.TotalPages);
        Assert.Equal(20, page1.Items.Count);
        Assert.Empty(page2.Items);
    }

    [Fact]
    public async Task LowStockOnly_True_NoMatchingRecords_ReturnsEmptyNotFullTable()
    {
        var service = CreateService(nameof(LowStockOnly_True_NoMatchingRecords_ReturnsEmptyNotFullTable), out var ctx);
        AddAll(ctx,
            NewItem("MI-OK-1", onHand: 100, reorderPoint: 10),
            NewItem("MI-OK-2", onHand: 200, reorderPoint: 10),
            NewItem("MI-NOSTOCK")); // بدون رکورد Stock اصلاً - IsLowStock باید false باشد
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25, lowStockOnly: true);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task LowStockOnly_True_DifferentPageSizes_TotalCountStaysConsistent(int pageSize)
    {
        var service = CreateService($"{nameof(LowStockOnly_True_DifferentPageSizes_TotalCountStaysConsistent)}_{pageSize}", out var ctx);
        for (var i = 0; i < 15; i++)
            AddAll(ctx, NewItem($"MI-LOW-{i:D3}", onHand: 1, reorderPoint: 10));
        for (var i = 0; i < 30; i++)
            AddAll(ctx, NewItem($"MI-OK-{i:D3}", onHand: 100, reorderPoint: 10));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize, lowStockOnly: true);

        Assert.Equal(15, result.TotalCount); // مستقل از pageSize
        Assert.Equal(15, result.Items.Count); // چون ۱۵ کمتر از هر سه اندازه‌ی صفحه است
    }

    [Fact]
    public async Task LowStockOnly_True_CombinedWithSearchAndCategoryFilters_AllApplyInSql()
    {
        var service = CreateService(nameof(LowStockOnly_True_CombinedWithSearchAndCategoryFilters_AllApplyInSql), out var ctx);
        ctx.MaterialCategories.Add(new MaterialCategory { MaterialCategoryId = 1, Name = "Bearings", IsActive = true });
        ctx.MaterialCategories.Add(new MaterialCategory { MaterialCategoryId = 2, Name = "Valves", IsActive = true });
        var (bearingLow, bearingStock) = NewItem("BRG-001", onHand: 1, reorderPoint: 10);
        bearingLow.CategoryId = 1;
        var (valveLow, valveStock) = NewItem("VLV-001", onHand: 1, reorderPoint: 10);
        valveLow.CategoryId = 2;
        AddAll(ctx, (bearingLow, bearingStock), (valveLow, valveStock));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25, q: "BRG", categoryId: 1, lowStockOnly: true);

        Assert.Single(result.Items);
        Assert.Equal("BRG-001", result.Items[0].MaterialCode);
    }

    [Fact]
    public async Task LowStockOnly_True_IsLowStockFormula_MatchesReorderPointBoundaryExactly()
    {
        // فرمول: ReorderPoint.HasValue && (OnHand - Reserved) <= ReorderPoint - مساوی هم کم‌موجودی است
        var service = CreateService(nameof(LowStockOnly_True_IsLowStockFormula_MatchesReorderPointBoundaryExactly), out var ctx);
        AddAll(ctx,
            NewItem("MI-EXACT", onHand: 10, reserved: 0, reorderPoint: 10),   // 10 <= 10 -> low
            NewItem("MI-ABOVE", onHand: 11, reserved: 0, reorderPoint: 10),   // 11 <= 10 -> false
            NewItem("MI-RESERVED", onHand: 20, reserved: 15, reorderPoint: 10)); // available=5 <= 10 -> low
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25, lowStockOnly: true);

        var codes = result.Items.Select(x => x.MaterialCode).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "MI-EXACT", "MI-RESERVED" }, codes);
    }

    [Fact]
    public async Task InactiveItems_AreExcluded_RegardlessOfLowStockFilter()
    {
        var service = CreateService(nameof(InactiveItems_AreExcluded_RegardlessOfLowStockFilter), out var ctx);
        var (activeLow, activeStock) = NewItem("MI-ACTIVE", onHand: 1, reorderPoint: 10);
        var (inactiveLow, inactiveStock) = NewItem("MI-INACTIVE", onHand: 1, reorderPoint: 10);
        inactiveLow.IsActive = false;
        AddAll(ctx, (activeLow, activeStock), (inactiveLow, inactiveStock));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 25, lowStockOnly: true);

        Assert.Single(result.Items);
        Assert.Equal("MI-ACTIVE", result.Items[0].MaterialCode);
    }
}
