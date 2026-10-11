using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P4 - یکتایی و قطعیت ردیف موجودی (InMemory): کلید (Item، Warehouse، Location؟) با NULL = «بدون محل»؛
/// یک ردیف ⇒ همان؛ هیچ ردیف ⇒ ساخت؛ چند ردیف ⇒ خطای صریح بدون هیچ تغییر (نه انتخاب دلخواه، نه جمع).
/// </summary>
public class StockRowIntegrityTests
{
    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public required MaterialStockService Svc { get; init; }
        public int Item, Item2, Wh, Wh2, Loc1, Loc2;
    }

    private static async Task<Env> NewAsync()
    {
        var db = PemsDbContextFactory.CreateInMemory("p4_" + Guid.NewGuid().ToString("N"));
        var wh = new Warehouse { Code = "WH-1", Name = "Main", IsActive = true };
        var wh2 = new Warehouse { Code = "WH-2", Name = "Second", IsActive = true };
        var i1 = new MaterialItem { MaterialCode = "M-1", Description = "Bearing", IsStockItem = true, IsActive = true };
        var i2 = new MaterialItem { MaterialCode = "M-2", Description = "Seal", IsStockItem = true, IsActive = true };
        db.AddRange(wh, wh2, i1, i2);
        await db.SaveChangesAsync();
        var l1 = new StorageLocation { WarehouseId = wh.WarehouseId, Code = "A-01", Name = "A1", IsActive = true };
        var l2 = new StorageLocation { WarehouseId = wh.WarehouseId, Code = "A-02", Name = "A2", IsActive = true };
        db.AddRange(l1, l2);
        await db.SaveChangesAsync();
        return new Env
        {
            Db = db, Svc = new MaterialStockService(db),
            Item = i1.MaterialItemId, Item2 = i2.MaterialItemId, Wh = wh.WarehouseId, Wh2 = wh2.WarehouseId,
            Loc1 = l1.StorageLocationId, Loc2 = l2.StorageLocationId
        };
    }

    private static MaterialStock Row(int item, int wh, int? loc, decimal onHand = 0, decimal reserved = 0, bool active = true)
        => new() { MaterialItemId = item, WarehouseId = wh, StorageLocationId = loc, OnHandQuantity = onHand, ReservedQuantity = reserved, IsActive = active };

    // ───────── جستجوی یکتا ─────────

    [Fact]
    public async Task FindExact_SingleRow_ReturnsThatRow()
    {
        var e = await NewAsync();
        var r = Row(e.Item, e.Wh, null, 5);
        e.Db.MaterialStocks.Add(r);
        await e.Db.SaveChangesAsync();

        var lookup = await StockRowResolver.FindExactAsync(e.Db, e.Item, e.Wh, null, activeOnly: true);

        Assert.False(lookup.IsAmbiguous);
        Assert.Equal(r.MaterialStockId, lookup.Row!.MaterialStockId);
    }

    [Fact]
    public async Task FindExact_NullLocation_NeverMatchesALocationRow_AndViceVersa()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Row(e.Item, e.Wh, e.Loc1, 7));
        await e.Db.SaveChangesAsync();

        var noLoc = await StockRowResolver.FindExactAsync(e.Db, e.Item, e.Wh, null, activeOnly: false);
        var withLoc = await StockRowResolver.FindExactAsync(e.Db, e.Item, e.Wh, e.Loc1, activeOnly: false);
        var otherLoc = await StockRowResolver.FindExactAsync(e.Db, e.Item, e.Wh, e.Loc2, activeOnly: false);

        Assert.Null(noLoc.Row); Assert.Empty(noLoc.CandidateIds);
        Assert.NotNull(withLoc.Row);
        Assert.Null(otherLoc.Row);
    }

    [Fact]
    public async Task FindExact_DuplicateNullLocationRows_IsAmbiguous_ListsBothIds_AndPicksNone()
    {
        var e = await NewAsync();
        var a = Row(e.Item, e.Wh, null, 5); var b = Row(e.Item, e.Wh, null, 9);
        e.Db.MaterialStocks.AddRange(a, b);
        await e.Db.SaveChangesAsync();

        var lookup = await StockRowResolver.FindExactAsync(e.Db, e.Item, e.Wh, null, activeOnly: false);

        Assert.True(lookup.IsAmbiguous);
        Assert.Null(lookup.Row);
        Assert.Equal(new[] { a.MaterialStockId, b.MaterialStockId }, lookup.CandidateIds);
    }

    [Fact]
    public async Task FindForWarehouse_MultiLocationRows_IsAmbiguous_SoNoRowIsSilentlyPicked()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Row(e.Item, e.Wh, e.Loc1, 5), Row(e.Item, e.Wh, e.Loc2, 9));
        await e.Db.SaveChangesAsync();

        var lookup = await StockRowResolver.FindForWarehouseAsync(e.Db, e.Item, e.Wh, activeOnly: true);

        Assert.True(lookup.IsAmbiguous);
        Assert.Null(lookup.Row);
    }

    [Fact]
    public async Task FindForWarehouse_InactiveDuplicateIsIgnoredWhenActiveOnly()
    {
        var e = await NewAsync();
        var live = Row(e.Item, e.Wh, null, 5);
        e.Db.MaterialStocks.AddRange(live, Row(e.Item, e.Wh, null, 9, active: false));
        await e.Db.SaveChangesAsync();

        var lookup = await StockRowResolver.FindForWarehouseAsync(e.Db, e.Item, e.Wh, activeOnly: true);

        Assert.False(lookup.IsAmbiguous);
        Assert.Equal(live.MaterialStockId, lookup.Row!.MaterialStockId);
    }

    // ───────── ساخت ردیف اول ─────────

    [Fact]
    public async Task Receive_MissingRow_CreatesExactlyOneRow_AndOneTransaction()
    {
        var e = await NewAsync();

        var (ok, err) = await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 12, "first", null);

        Assert.True(ok, err);
        var row = await e.Db.MaterialStocks.SingleAsync(x => x.MaterialItemId == e.Item);
        Assert.Equal(12m, row.OnHandQuantity);
        Assert.Single(await e.Db.InventoryTransactions.Where(t => t.MaterialItemId == e.Item).ToListAsync());
    }

    [Fact]
    public async Task Receive_RepeatedCalls_ReuseTheSameRow_AndAccumulate()
    {
        var e = await NewAsync();

        await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 5, "r1", null);
        await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 7, "r2", null);

        var row = await e.Db.MaterialStocks.SingleAsync(x => x.MaterialItemId == e.Item);
        Assert.Equal(12m, row.OnHandQuantity);
        Assert.Equal(2, await e.Db.InventoryTransactions.CountAsync(t => t.MaterialItemId == e.Item));
    }

    [Fact]
    public async Task Receive_IntoDuplicateNullLocationRows_FailsSafely_NothingChanges()
    {
        var e = await NewAsync();
        var a = Row(e.Item, e.Wh, null, 5); var b = Row(e.Item, e.Wh, null, 9);
        e.Db.MaterialStocks.AddRange(a, b);
        await e.Db.SaveChangesAsync();

        var (ok, err) = await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 4, "dup", null);

        Assert.False(ok);
        Assert.Contains(a.MaterialStockId.ToString(), err);
        Assert.Contains(b.MaterialStockId.ToString(), err);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(new[] { 5m, 9m }, (await e.Db.MaterialStocks.OrderBy(x => x.MaterialStockId).Select(x => x.OnHandQuantity).ToListAsync()).ToArray());
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task ConflictingDuplicateBalances_AreNeverSummedOrPicked_ByAdjustReturnAndTransfer()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Row(e.Item, e.Wh, null, 5), Row(e.Item, e.Wh, null, 900));
        await e.Db.SaveChangesAsync();

        var adjust = await e.Svc.AdjustAsync(e.Item, e.Wh, null, 50, "adj", null);
        var ret = await e.Svc.ReturnAsync(e.Item, e.Wh, null, 3, "ret", null);
        var transfer = await e.Svc.TransferAsync(e.Item, e.Wh, null, e.Wh2, null, 2, "tr", null);

        Assert.False(adjust.Success); Assert.False(ret.Success); Assert.False(transfer.Success);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(new[] { 5m, 900m }, (await e.Db.MaterialStocks.OrderBy(x => x.MaterialStockId).Select(x => x.OnHandQuantity).ToListAsync()).ToArray());
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
        Assert.Equal(2, await e.Db.MaterialStocks.CountAsync());
    }

    // ───────── چند انبار / محل ─────────

    [Fact]
    public async Task MultipleWarehouses_AreIndependentIdentities()
    {
        var e = await NewAsync();

        await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 5, "a", null);
        await e.Svc.ReceiveAsync(e.Item, e.Wh2, null, 8, "b", null);

        var rows = await e.Db.MaterialStocks.Where(x => x.MaterialItemId == e.Item).OrderBy(x => x.WarehouseId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 5m, 8m }, rows.Select(r => r.OnHandQuantity).ToArray());
    }

    [Fact]
    public async Task NullAndNonNullLocations_AreSeparateIdentities_AndEachIsReusedOnRepeat()
    {
        var e = await NewAsync();

        await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 1, "n", null);
        await e.Svc.ReceiveAsync(e.Item, e.Wh, e.Loc1, 2, "l1", null);
        await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 4, "n2", null);
        await e.Svc.ReceiveAsync(e.Item, e.Wh, e.Loc1, 8, "l1b", null);

        var rows = await e.Db.MaterialStocks.Where(x => x.MaterialItemId == e.Item).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(5m, rows.Single(r => r.StorageLocationId == null).OnHandQuantity);
        Assert.Equal(10m, rows.Single(r => r.StorageLocationId == e.Loc1).OnHandQuantity);
    }

    [Fact]
    public async Task Transfer_MovesBetweenResolvedRows_AndWritesBothLedgerLegs()
    {
        var e = await NewAsync();
        await e.Svc.ReceiveAsync(e.Item, e.Wh, null, 10, "seed", null);

        var (ok, err) = await e.Svc.TransferAsync(e.Item, e.Wh, null, e.Wh2, null, 4, "move", null);

        Assert.True(ok, err);
        Assert.Equal(6m, (await e.Db.MaterialStocks.SingleAsync(x => x.WarehouseId == e.Wh)).OnHandQuantity);
        Assert.Equal(4m, (await e.Db.MaterialStocks.SingleAsync(x => x.WarehouseId == e.Wh2)).OnHandQuantity);
        Assert.Equal(3, await e.Db.InventoryTransactions.CountAsync()); // seed receipt + 2 transfer legs
    }

    [Fact]
    public async Task Transfer_FromMissingRow_IsRejected_AndCreatesNoSourceRow()
    {
        var e = await NewAsync();

        var (ok, _) = await e.Svc.TransferAsync(e.Item, e.Wh, null, e.Wh2, null, 4, "move", null);

        Assert.False(ok);
        Assert.Empty(await e.Db.MaterialStocks.Where(x => x.WarehouseId == e.Wh).ToListAsync());
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
    }

    // ───────── AdjustBy (کاهش/افزایش نسبی - Workstream D) ─────────

    [Fact]
    public async Task AdjustBy_NegativeDelta_ConsumesFromTheRow_AndWritesASignedAdjustmentTransaction()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e.Item, e.Wh, null, 10));
        await e.Db.SaveChangesAsync();

        var (ok, err) = await e.Svc.AdjustByAsync(e.Item, e.Wh, null, -4, "use", null, "ProductionInput", 7);

        Assert.True(ok, err);
        Assert.Equal(6m, (await e.Db.MaterialStocks.SingleAsync()).OnHandQuantity);
        var tx = await e.Db.InventoryTransactions.SingleAsync();
        Assert.Equal(("Adjustment", -4m, "ProductionInput", (int?)7), (tx.TransactionType, tx.Quantity, tx.ReferenceType, tx.ReferenceId));
    }

    [Fact]
    public async Task AdjustBy_Consumption_WithoutEnoughStockOrRow_FailsAndCreatesNothing()
    {
        var e = await NewAsync();

        var missing = await e.Svc.AdjustByAsync(e.Item, e.Wh, null, -1, "use", null);
        Assert.False(missing.Success);
        Assert.Empty(await e.Db.MaterialStocks.ToListAsync());

        e.Db.MaterialStocks.Add(Row(e.Item, e.Wh, null, 3));
        await e.Db.SaveChangesAsync();
        var tooMuch = await e.Svc.AdjustByAsync(e.Item, e.Wh, null, -4, "use", null);
        Assert.False(tooMuch.Success);
        Assert.Contains("3", tooMuch.Error);
        Assert.Equal(3m, (await e.Db.MaterialStocks.SingleAsync()).OnHandQuantity);
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task AdjustBy_RefusesToGoBelowReserved_AndRefusesAmbiguousRows()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e.Item, e.Wh, null, 10, reserved: 8));
        await e.Db.SaveChangesAsync();
        Assert.False((await e.Svc.AdjustByAsync(e.Item, e.Wh, null, -5, "use", null)).Success);

        e.Db.MaterialStocks.Add(Row(e.Item, e.Wh, null, 50));
        await e.Db.SaveChangesAsync();
        var ambiguous = await e.Svc.AdjustByAsync(e.Item, e.Wh, null, -1, "use", null);
        Assert.False(ambiguous.Success);
        Assert.Contains("MaterialStockId", ambiguous.Error);
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task AdjustBy_PositiveDeltaOnMissingRow_CreatesIt_ZeroDeltaIsANoOp()
    {
        var e = await NewAsync();
        Assert.True((await e.Svc.AdjustByAsync(e.Item, e.Wh, null, 0, "none", null)).Success);
        Assert.Empty(await e.Db.MaterialStocks.ToListAsync());

        Assert.True((await e.Svc.AdjustByAsync(e.Item, e.Wh, null, 5, "add", null)).Success);
        Assert.Equal(5m, (await e.Db.MaterialStocks.SingleAsync()).OnHandQuantity);
    }

    // ───────── رزرو/تحویل موجود دست‌نخورده ─────────

    [Fact]
    public async Task Adjust_BelowReserved_IsStillRejected_ExistingReservationRuleUnchanged()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e.Item, e.Wh, null, 10, reserved: 6));
        await e.Db.SaveChangesAsync();

        var (ok, err) = await e.Svc.AdjustAsync(e.Item, e.Wh, null, 4, "x", null);

        Assert.False(ok);
        Assert.Contains("6", err);
        Assert.Equal(10m, (await e.Db.MaterialStocks.SingleAsync()).OnHandQuantity);
    }

    [Fact]
    public async Task Adjust_SameValue_IsIdempotent_NoTransactionWritten()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Row(e.Item, e.Wh, null, 10));
        await e.Db.SaveChangesAsync();

        var first = await e.Svc.AdjustAsync(e.Item, e.Wh, null, 10, "same", null);
        var second = await e.Svc.AdjustAsync(e.Item, e.Wh, null, 10, "same", null);

        Assert.True(first.Success); Assert.True(second.Success);
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
    }
}
