using Microsoft.EntityFrameworkCore;
using PEMS.Application.Inventory;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>Phase 2 / P1 - موجودی قابل‌استفاده: فقط‌خواندنی، بدون انتخاب دلخواه، بدون Clamp/اصلاح داده، با گزارش صریح موارد غیرعادی.</summary>
public class UsableStockServiceTests
{
    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public required UsableStockService Svc { get; init; }
        public int Each, Item, Wh;
    }

    private static async Task<Env> NewAsync()
    {
        var db = PemsDbContextFactory.CreateInMemory("usable_" + Guid.NewGuid().ToString("N"));
        var uom = new MaterialUOM { Code = "EA", Name = "Each" };
        var wh = new Warehouse { Code = "WH-1", Name = "Main", IsActive = true };
        db.AddRange(uom, wh);
        await db.SaveChangesAsync();
        var item = new MaterialItem { MaterialCode = "M-1", Description = "Bearing", UomId = uom.MaterialUOMId, IsStockItem = true, IsActive = true };
        db.MaterialItems.Add(item);
        await db.SaveChangesAsync();
        return new Env { Db = db, Svc = new UsableStockService(db), Each = uom.MaterialUOMId, Item = item.MaterialItemId, Wh = wh.WarehouseId };
    }

    private static MaterialStock Row(Env e, decimal onHand, decimal reserved, int? location = null, bool active = true,
        decimal? min = null, decimal? max = null, decimal? rop = null, decimal? roq = null)
        => new() { MaterialItemId = e.Item, WarehouseId = e.Wh, StorageLocationId = location, OnHandQuantity = onHand, ReservedQuantity = reserved, IsActive = active,
            MinimumQuantity = min, MaximumQuantity = max, ReorderPoint = rop, ReorderQuantity = roq };

    private static async Task<UsableStockResult> OneAsync(Env e) => Assert.Single(await e.Svc.GetUsableStockAsync(new[] { new StockKey(e.Item, e.Wh) }));
    private static bool Has(UsableStockResult r, NetDemandIssueCode code) => r.Issues.Any(i => i.Code == code);

    [Fact]
    public async Task SingleActiveRow_UsableIsOnHandMinusReserved_AndPolicyIsEchoed()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e, 10, 3, min: 2, max: 40, rop: 5, roq: 20));
        await e.Db.SaveChangesAsync();

        var r = await OneAsync(e);

        Assert.Equal(StockState.Ok, r.State);
        Assert.Equal(10m, r.OnHand);
        Assert.Equal(3m, r.Reserved);
        Assert.Equal(7m, r.RawUsable);
        Assert.Equal(new ReorderPolicy(2, 40, 5, 20), r.Policy);
        Assert.False(r.HasBlockingIssue);
    }

    [Fact]
    public async Task MissingStockRow_IsZeroStock_AsTheReservationServiceTreatsIt_AndNonBlocking()
    {
        var e = await NewAsync();

        var r = await OneAsync(e);

        Assert.Equal(StockState.NoStockRecord, r.State);
        Assert.Equal(0m, r.RawUsable);
        Assert.True(Has(r, NetDemandIssueCode.StockNoRecord));
        Assert.False(r.HasBlockingIssue);
    }

    [Fact]
    public async Task MaterialAndWarehouseProblems_AreExplicitAndNotCalculable()
    {
        var e = await NewAsync();
        var inactive = new MaterialItem { MaterialCode = "M-OFF", Description = "x", UomId = e.Each, IsActive = false };
        var nonStock = new MaterialItem { MaterialCode = "M-NS", Description = "x", UomId = e.Each, IsStockItem = false };
        var noUnit = new MaterialItem { MaterialCode = "M-NU", Description = "x", UomId = null };
        var offWh = new Warehouse { Code = "WH-OFF", Name = "Off", IsActive = false };
        e.Db.AddRange(inactive, nonStock, noUnit, offWh);
        await e.Db.SaveChangesAsync();

        var res = await e.Svc.GetUsableStockAsync(new[]
        {
            new StockKey(inactive.MaterialItemId, e.Wh), new StockKey(nonStock.MaterialItemId, e.Wh), new StockKey(noUnit.MaterialItemId, e.Wh),
            new StockKey(999_999, e.Wh), new StockKey(e.Item, offWh.WarehouseId), new StockKey(e.Item, 888_888)
        });

        Assert.All(res, r => Assert.Equal(StockState.NotCalculable, r.State));
        Assert.Contains(res, r => r.MaterialItemId == inactive.MaterialItemId && Has(r, NetDemandIssueCode.InactiveMaterial));
        Assert.Contains(res, r => r.MaterialItemId == nonStock.MaterialItemId && Has(r, NetDemandIssueCode.NotStockItem));
        Assert.Contains(res, r => r.MaterialItemId == noUnit.MaterialItemId && Has(r, NetDemandIssueCode.UnitUnknown));
        Assert.Contains(res, r => r.MaterialItemId == 999_999 && Has(r, NetDemandIssueCode.MaterialNotFound));
        Assert.Contains(res, r => r.WarehouseId == offWh.WarehouseId && Has(r, NetDemandIssueCode.WarehouseInactive));
        Assert.Contains(res, r => r.WarehouseId == 888_888 && Has(r, NetDemandIssueCode.WarehouseNotFound));
    }

    [Fact]
    public async Task DuplicateRowsWithoutLocation_AreAmbiguous_AndNoRowIsPickedArbitrarily()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Row(e, 10, 0), Row(e, 4, 4));
        await e.Db.SaveChangesAsync();

        var r = await OneAsync(e);

        Assert.Equal(StockState.AmbiguousRows, r.State);
        Assert.Null(r.OnHand);
        Assert.Null(r.RawUsable);
        Assert.Equal(2, r.ActiveRowCount);
        Assert.True(Has(r, NetDemandIssueCode.StockAmbiguousRows));
        Assert.True(r.HasBlockingIssue);
    }

    [Fact]
    public async Task RowsInDifferentLocations_AreAggregatedAtWarehouseLevel_RegardlessOfWhichRowHoldsTheReservation()
    {
        var e = await NewAsync();
        var l1 = new StorageLocation { Code = "A", Name = "A", WarehouseId = e.Wh };
        var l2 = new StorageLocation { Code = "B", Name = "B", WarehouseId = e.Wh };
        e.Db.StorageLocations.AddRange(l1, l2);
        await e.Db.SaveChangesAsync();
        e.Db.MaterialStocks.AddRange(Row(e, 6, 5, l1.StorageLocationId), Row(e, 4, 0, l2.StorageLocationId));
        await e.Db.SaveChangesAsync();

        var r = await OneAsync(e);

        Assert.Equal(StockState.Ok, r.State);
        Assert.Equal(10m, r.OnHand);
        Assert.Equal(5m, r.Reserved);
        Assert.Equal(5m, r.RawUsable);
        Assert.True(Has(r, NetDemandIssueCode.StockMultipleLocationRows));
        Assert.False(r.HasBlockingIssue);
        Assert.Null(r.Policy);   // سیاست چند ردیف مبهم است
    }

    [Fact]
    public async Task NegativeAndOverReservedBalances_AreReported_NeverClamped_AndNeverModified()
    {
        var e = await NewAsync();
        var stock = Row(e, -3, 0);
        e.Db.MaterialStocks.Add(stock);
        await e.Db.SaveChangesAsync();

        var negative = await OneAsync(e);
        Assert.Equal(-3m, negative.OnHand);
        Assert.Equal(-3m, negative.RawUsable);
        Assert.True(Has(negative, NetDemandIssueCode.StockNegativeOnHand));
        Assert.True(negative.HasBlockingIssue);

        stock.OnHandQuantity = 5;
        stock.ReservedQuantity = 8;
        await e.Db.SaveChangesAsync();
        var over = await OneAsync(e);
        Assert.Equal(-3m, over.RawUsable);
        Assert.True(Has(over, NetDemandIssueCode.StockReservedExceedsOnHand));

        var persisted = await e.Db.MaterialStocks.AsNoTracking().SingleAsync();
        Assert.Equal(5m, persisted.OnHandQuantity);
        Assert.Equal(8m, persisted.ReservedQuantity);
    }

    [Fact]
    public async Task InactiveRowWithQuantity_IsExcluded_ButFlaggedBecauseUsableStockMayBeUnderstated()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Row(e, 10, 0, active: false), Row(e, 2, 0, location: null));
        await e.Db.SaveChangesAsync();

        var r = await OneAsync(e);

        Assert.Equal(2m, r.OnHand);
        Assert.True(Has(r, NetDemandIssueCode.StockInactiveRowHasQuantity));
        Assert.True(r.HasBlockingIssue);
    }

    [Fact]
    public async Task OnlyInactiveRows_AreTreatedAsNoStockRecord_WithTheInactiveQuantityFlagged()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e, 10, 0, active: false));
        await e.Db.SaveChangesAsync();

        var r = await OneAsync(e);

        Assert.Equal(StockState.NoStockRecord, r.State);
        Assert.True(Has(r, NetDemandIssueCode.StockInactiveRowHasQuantity));
    }

    [Fact]
    public async Task Query_IsReadOnly_NoStockTransactionReservationOrStatusChanges()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e, 10, 2));
        await e.Db.SaveChangesAsync();
        var before = await Snapshot(e);
        e.Db.ChangeTracker.Clear();

        await e.Svc.GetUsableStockAsync(new[] { new StockKey(e.Item, e.Wh), new StockKey(e.Item, e.Wh) });

        Assert.False(e.Db.ChangeTracker.HasChanges());
        Assert.Empty(e.Db.ChangeTracker.Entries());               // AsNoTracking: هیچ Entity ردیابی نشد
        Assert.Equal(before, await Snapshot(e));
    }

    [Fact]
    public async Task EmptyOrDuplicateKeys_AreHandled()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e, 3, 1));
        await e.Db.SaveChangesAsync();

        Assert.Empty(await e.Svc.GetUsableStockAsync(Array.Empty<StockKey>()));
        Assert.Single(await e.Svc.GetUsableStockAsync(new[] { new StockKey(e.Item, e.Wh), new StockKey(e.Item, e.Wh) }));
    }

    private static async Task<string> Snapshot(Env e) =>
        string.Join("|",
            await e.Db.MaterialStocks.AsNoTracking().Select(s => $"{s.MaterialStockId}:{s.OnHandQuantity}:{s.ReservedQuantity}:{s.IsActive}").ToListAsync())
        + "#" + await e.Db.InventoryTransactions.CountAsync() + "#" + await e.Db.MaterialReservations.CountAsync();
}
