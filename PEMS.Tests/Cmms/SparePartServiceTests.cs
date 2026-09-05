using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۵ - SparePart): قبلاً
/// SparePartController.Index کل جدول را دو بار با ToListAsync (بدون Skip/Take) می‌خواند -
/// یک بار برای Grid، یک بار جداگانه فقط برای شمارنده‌ی LowStockCount - و فیلتر lowStockOnly را
/// در حافظه اعمال می‌کرد. این تست‌ها ثابت می‌کنند GetListAsync/GetLowStockCountAsync حالا در
/// SQL صفحه‌بندی/فیلتر/شمارش می‌کنند و رفتار قبلی (LowStockCount روی کل قطعات فعال، نه محدود
/// به فیلتر q/lowStockOnly صفحه) حفظ شده.
/// </summary>
public class SparePartServiceTests
{
    private static SparePartService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        // پیشوند نام کلاس - نگاه کنید InstrumentServiceTests.CreateService برای دلیل کامل
        // (EF Core InMemory دیتابیس‌ها را با نام سراسری در کل فرآیند تست نگه می‌دارد).
        ctx = PemsDbContextFactory.CreateInMemory($"SparePart_{dbName}");
        return new SparePartService(ctx);
    }

    private static SparePart NewPart(string partNumber, string partName, decimal qty, decimal? reorderPoint, bool isActive = true) => new()
    {
        PartNumber = partNumber,
        PartName = partName,
        QuantityOnHand = qty,
        ReorderPoint = reorderPoint,
        IsActive = isActive
    };

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.SpareParts.Add(NewPart($"P-{i:D3}", $"Part {i}", 10, null));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetListAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetListAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.SpareParts.Add(NewPart($"P-{i:D3}", $"Part {i}", 10, null));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.SpareParts.Add(NewPart("P-001", "Bearing", 10, null));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(page: 1, pageSize: 13);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.SpareParts.Add(NewPart($"P-{i:D3}", $"Part {i}", 10, null));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(page: 1, pageSize: 20);
        var page2 = await service.GetListAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.SparePartId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        for (var i = 0; i < 30; i++)
            ctx.SpareParts.Add(NewPart($"P-{i:D3}", $"Part {i}", 10, null));
        await ctx.SaveChangesAsync();

        var first = await service.GetListAsync(page: 1, pageSize: 20);
        var second = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.SparePartId), second.Items.Select(x => x.SparePartId));
    }

    [Fact]
    public async Task GetListAsync_LowStockOnlyFilter_MatchesReorderPointFormula()
    {
        var service = CreateService(nameof(GetListAsync_LowStockOnlyFilter_MatchesReorderPointFormula), out var ctx);
        ctx.SpareParts.Add(NewPart("P-LOW", "Low stock part", qty: 2, reorderPoint: 5)); // 2 <= 5 -> low
        ctx.SpareParts.Add(NewPart("P-OK", "OK part", qty: 20, reorderPoint: 5));        // 20 > 5 -> not low
        ctx.SpareParts.Add(NewPart("P-NOREORDER", "No reorder point", qty: 0, reorderPoint: null)); // never low
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(lowStockOnly: true);

        Assert.Single(result.Items);
        Assert.Equal("P-LOW", result.Items[0].PartNumber);
        Assert.True(result.Items[0].IsLowStock);
    }

    [Fact]
    public async Task GetListAsync_SearchFilter_MatchesPartNumberOrPartName()
    {
        var service = CreateService(nameof(GetListAsync_SearchFilter_MatchesPartNumberOrPartName), out var ctx);
        ctx.SpareParts.Add(NewPart("BRG-6312", "Deep Groove Bearing", 10, null));
        ctx.SpareParts.Add(NewPart("SEAL-100", "Mechanical Seal", 10, null));
        await ctx.SaveChangesAsync();

        var byPartNo = await service.GetListAsync(q: "brg-6312");
        var byName = await service.GetListAsync(q: "Bearing");

        Assert.Single(byPartNo.Items);
        Assert.Single(byName.Items);
        Assert.Equal("BRG-6312", byPartNo.Items[0].PartNumber);
    }

    [Fact]
    public async Task GetListAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetListAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetListAsync();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetListAsync_Projection_MapsFieldsCorrectly()
    {
        var service = CreateService(nameof(GetListAsync_Projection_MapsFieldsCorrectly), out var ctx);
        var part = NewPart("BRG-6312", "Deep Groove Bearing", qty: 3, reorderPoint: 10);
        part.Manufacturer = "SKF";
        part.Model = "6312-2RS1";
        part.Unit = "PCS";
        part.StorageLocation = "Warehouse-A-Rack3";
        part.MinStockLevel = 5;
        ctx.SpareParts.Add(part);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();
        var dto = Assert.Single(result.Items);

        Assert.Equal(part.SparePartId, dto.SparePartId);
        Assert.Equal("BRG-6312", dto.PartNumber);
        Assert.Equal("SKF", dto.Manufacturer);
        Assert.Equal("6312-2RS1", dto.Model);
        Assert.Equal("PCS", dto.Unit);
        Assert.Equal("Warehouse-A-Rack3", dto.StorageLocation);
        Assert.Equal(5, dto.MinStockLevel);
        Assert.True(dto.IsLowStock);
    }

    [Fact]
    public async Task GetListAsync_InactiveParts_AreExcluded()
    {
        var service = CreateService(nameof(GetListAsync_InactiveParts_AreExcluded), out var ctx);
        ctx.SpareParts.Add(NewPart("P-ACTIVE", "Active part", 10, null, isActive: true));
        ctx.SpareParts.Add(NewPart("P-INACTIVE", "Inactive part", 10, null, isActive: false));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync();

        Assert.Single(result.Items);
        Assert.Equal("P-ACTIVE", result.Items[0].PartNumber);
    }

    [Fact]
    public async Task GetLowStockCountAsync_IgnoresSearchAndLowStockOnlyFilters_CountsAllActiveLowStock()
    {
        var service = CreateService(nameof(GetLowStockCountAsync_IgnoresSearchAndLowStockOnlyFilters_CountsAllActiveLowStock), out var ctx);
        ctx.SpareParts.Add(NewPart("P-LOW1", "Low 1", 1, 5));
        ctx.SpareParts.Add(NewPart("P-LOW2", "Low 2", 2, 5));
        ctx.SpareParts.Add(NewPart("P-OK", "OK", 20, 5));
        ctx.SpareParts.Add(NewPart("P-INACTIVE-LOW", "Inactive low", 1, 5, isActive: false));
        await ctx.SaveChangesAsync();

        var count = await service.GetLowStockCountAsync();

        Assert.Equal(2, count);
    }
}
