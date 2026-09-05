using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق «PEMS Phase 6B — P1: InventoryTransaction Grid». قبلاً pageSize=30 در Controller
/// مستقیم Hardcode شده بود و View یک Pager دستی (for-loop، بدون حفظ فیلترهای فعال) داشت -
/// جایگزین‌شده با _Pagination.cshtml مشترک.
/// </summary>
public class InventoryTransactionGridPaginationTests
{
    private static InventoryTransactionService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"InvTxnGrid_{dbName}");
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 1, MaterialCode = "MI-001", Description = "Test Material", IsActive = true });
        ctx.Warehouses.Add(new Warehouse { WarehouseId = 1, Code = "WH1", Name = "Main Warehouse", IsActive = true });
        ctx.SaveChanges();
        return new InventoryTransactionService(ctx);
    }

    private static int _nextId = 1;

    private static InventoryTransaction NewTxn(DateTime date, string type = "Receipt") => new()
    {
        InventoryTransactionId = _nextId++,
        MaterialItemId = 1,
        WarehouseId = 1,
        TransactionType = type,
        Quantity = 10,
        TransactionDate = date
    };

    [Fact]
    public async Task GetLedgerAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetLedgerAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.InventoryTransactions.Add(NewTxn(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetLedgerAsync(page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetLedgerAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetLedgerAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.InventoryTransactions.Add(NewTxn(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await service.GetLedgerAsync(page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetLedgerAsync_InvalidPageSize_FallsBackTo50()
    {
        // دقیقاً مقدار ۳۰ که قبلاً Hardcode شده بود
        var service = CreateService(nameof(GetLedgerAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.InventoryTransactions.Add(NewTxn(DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await service.GetLedgerAsync(page: 1, pageSize: 30);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetLedgerAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetLedgerAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.InventoryTransactions.Add(NewTxn(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetLedgerAsync(page: 1, pageSize: 20);
        var page2 = await service.GetLedgerAsync(page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.InventoryTransactionId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetLedgerAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice()
    {
        var service = CreateService(nameof(GetLedgerAsync_DeterministicOrdering_SameQueryReturnsSameOrderTwice), out var ctx);
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.InventoryTransactions.Add(NewTxn(sameDate));
        await ctx.SaveChangesAsync();

        var first = await service.GetLedgerAsync(page: 1, pageSize: 20);
        var second = await service.GetLedgerAsync(page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.InventoryTransactionId), second.Items.Select(x => x.InventoryTransactionId));
    }

    [Fact]
    public async Task GetLedgerAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetLedgerAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetLedgerAsync();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetLedgerAsync_TransactionTypeFilter_StillWorksAfterPageSizeChange()
    {
        var service = CreateService(nameof(GetLedgerAsync_TransactionTypeFilter_StillWorksAfterPageSizeChange), out var ctx);
        ctx.InventoryTransactions.Add(NewTxn(DateTime.Today, type: "Receipt"));
        ctx.InventoryTransactions.Add(NewTxn(DateTime.Today, type: "Issue"));
        await ctx.SaveChangesAsync();

        var result = await service.GetLedgerAsync(transactionType: "Receipt");

        Assert.Single(result.Items);
        Assert.Equal("Receipt", result.Items[0].TransactionType);
    }
}
