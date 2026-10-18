using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P4 (Audit B) - برگشت قطعه‌ی Work Order فقط به انبار/محل مبدأ ثبت‌شده در تراکنش Issue؛ بدون حدس «بیشترین موجودی».
/// </summary>
public class WorkOrderSpareReturnTests
{
    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public int Item, WhA, WhB, Loc, Wo1, Wo2;
    }

    private static async Task<Env> NewAsync()
    {
        var db = PemsDbContextFactory.CreateInMemory("p4wo_" + Guid.NewGuid().ToString("N"));
        var a = new Warehouse { Code = "WH-A", Name = "A", IsActive = true };
        var b = new Warehouse { Code = "WH-B", Name = "B", IsActive = true };
        var item = new MaterialItem { MaterialCode = "M-1", Description = "Seal", IsStockItem = true, IsActive = true };
        var wo1 = new WorkOrder { WorkOrderNumber = "WO-1", EquipmentId = 1, Status = "Open" };
        var wo2 = new WorkOrder { WorkOrderNumber = "WO-2", EquipmentId = 1, Status = "Open" };
        db.AddRange(a, b, item, wo1, wo2);
        await db.SaveChangesAsync();
        var loc = new StorageLocation { WarehouseId = b.WarehouseId, Code = "B-01", Name = "B1", IsActive = true };
        db.StorageLocations.Add(loc);
        await db.SaveChangesAsync();
        return new Env { Db = db, Item = item.MaterialItemId, WhA = a.WarehouseId, WhB = b.WarehouseId, Loc = loc.StorageLocationId, Wo1 = wo1.WorkOrderId, Wo2 = wo2.WorkOrderId };
    }

    private static MaterialStock Stock(Env e, int wh, int? loc, decimal onHand) =>
        new() { MaterialItemId = e.Item, WarehouseId = wh, StorageLocationId = loc, OnHandQuantity = onHand, IsActive = true };

    /// <summary>همان ثبتی که AddSpareUsage می‌کند: Usage + Issue(ReferenceType=WorkOrder, WorkOrderId) از یک کلید مشخص.</summary>
    private static async Task<int> IssueAsync(Env e, int wo, int wh, int? loc, decimal qty, decimal? unitCost = 10)
    {
        var usage = new MaintenanceSpareUsage { WorkOrderId = wo, MaterialItemId = e.Item, Quantity = qty, UnitCost = unitCost, TotalCost = unitCost * qty, IsActive = true };
        e.Db.MaintenanceSpareUsages.Add(usage);
        e.Db.InventoryTransactions.Add(new InventoryTransaction
        {
            MaterialItemId = e.Item, WarehouseId = wh, StorageLocationId = loc, TransactionType = "Issue", Quantity = qty,
            ReferenceType = "WorkOrder", ReferenceId = wo, WorkOrderId = wo
        });
        await e.Db.SaveChangesAsync();
        return usage.MaintenanceSpareUsageId;
    }

    [Fact]
    public async Task Return_GoesToTheOriginalWarehouseAndLocation_NotToTheRowWithTheHighestOnHand()
    {
        var e = await NewAsync();
        var origin = Stock(e, e.WhB, e.Loc, 2);          // مبدأ: کم‌موجودی
        var richer = Stock(e, e.WhA, null, 500);          // ردیف دیگر با موجودی بیشتر (هدف رفتار قبلی)
        e.Db.MaterialStocks.AddRange(origin, richer);
        await e.Db.SaveChangesAsync();
        var usageId = await IssueAsync(e, e.Wo1, e.WhB, e.Loc, 5);

        var (ok, err, notFound) = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 3, null, null);

        Assert.True(ok, err); Assert.False(notFound);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(5m, (await e.Db.MaterialStocks.SingleAsync(s => s.MaterialStockId == origin.MaterialStockId)).OnHandQuantity);
        Assert.Equal(500m, (await e.Db.MaterialStocks.SingleAsync(s => s.MaterialStockId == richer.MaterialStockId)).OnHandQuantity);
        var ret = await e.Db.InventoryTransactions.SingleAsync(t => t.TransactionType == "Return");
        Assert.Equal((e.WhB, (int?)e.Loc), (ret.WarehouseId, ret.StorageLocationId));
        var usage = await e.Db.MaintenanceSpareUsages.SingleAsync();
        Assert.Equal(3m, usage.ReturnedQuantity);
        Assert.Equal(20m, usage.TotalCost);               // 10 × (5 − 3)
    }

    [Fact]
    public async Task Return_WithNoLocationOrigin_StaysInTheNullLocationRow_EvenWhenALocationRowHoldsMore()
    {
        var e = await NewAsync();
        var locA = new StorageLocation { WarehouseId = e.WhA, Code = "A-01", Name = "A1", IsActive = true };
        e.Db.StorageLocations.Add(locA);
        await e.Db.SaveChangesAsync();
        var nullRow = Stock(e, e.WhA, null, 1);
        var locRow = Stock(e, e.WhA, locA.StorageLocationId, 900);
        e.Db.MaterialStocks.AddRange(nullRow, locRow);
        await e.Db.SaveChangesAsync();
        var usageId = await IssueAsync(e, e.Wo1, e.WhA, null, 4);

        var (ok, err, _) = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 2, null, null);

        Assert.True(ok, err);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(3m, (await e.Db.MaterialStocks.SingleAsync(s => s.MaterialStockId == nullRow.MaterialStockId)).OnHandQuantity);
        Assert.Equal(900m, (await e.Db.MaterialStocks.SingleAsync(s => s.MaterialStockId == locRow.MaterialStockId)).OnHandQuantity);
    }

    [Fact]
    public async Task Return_WhenIssuesCameFromSeveralWarehouses_FailsWithDiagnostics_AndChangesNothing()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Stock(e, e.WhA, null, 10), Stock(e, e.WhB, e.Loc, 10));
        await e.Db.SaveChangesAsync();
        var usageId = await IssueAsync(e, e.Wo1, e.WhA, null, 4);
        await IssueAsync(e, e.Wo1, e.WhB, e.Loc, 2);

        var (ok, err, _) = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 1, null, null);

        Assert.False(ok);
        Assert.Contains("چند انبار", err);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(new[] { 10m, 10m }, (await e.Db.MaterialStocks.OrderBy(s => s.MaterialStockId).Select(s => s.OnHandQuantity).ToListAsync()).ToArray());
        Assert.DoesNotContain(await e.Db.InventoryTransactions.ToListAsync(), t => t.TransactionType == "Return");
        Assert.Null((await e.Db.MaintenanceSpareUsages.OrderBy(u => u.MaintenanceSpareUsageId).FirstAsync()).ReturnedQuantity);
    }

    [Fact]
    public async Task Return_WhenNoIssueTransactionExists_FailsInsteadOfGuessing()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Stock(e, e.WhA, null, 10));
        var usage = new MaintenanceSpareUsage { WorkOrderId = e.Wo1, MaterialItemId = e.Item, Quantity = 4, IsActive = true };
        e.Db.MaintenanceSpareUsages.Add(usage);
        await e.Db.SaveChangesAsync();

        var (ok, err, _) = await WorkOrderSpareReturn.ReturnAsync(e.Db, usage.MaintenanceSpareUsageId, e.Wo1, 1, null, null);

        Assert.False(ok);
        Assert.Contains("Issue", err);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(10m, (await e.Db.MaterialStocks.SingleAsync()).OnHandQuantity);
        Assert.Empty(await e.Db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task Return_ForAUsageOfAnotherWorkOrder_IsNotFound_AndIssuesOfOtherWorkOrdersAreIgnored()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Stock(e, e.WhA, null, 10), Stock(e, e.WhB, e.Loc, 10));
        await e.Db.SaveChangesAsync();
        var usageWo1 = await IssueAsync(e, e.Wo1, e.WhA, null, 4);
        await IssueAsync(e, e.Wo2, e.WhB, e.Loc, 4);      // Issue مربوط به WO دیگر نباید ابهام بسازد

        var wrong = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageWo1, e.Wo2, 1, null, null);
        Assert.True(wrong.NotFound);

        var right = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageWo1, e.Wo1, 1, null, null);
        Assert.True(right.Success, right.Error);
        e.Db.ChangeTracker.Clear();
        Assert.Equal(11m, (await e.Db.MaterialStocks.SingleAsync(s => s.WarehouseId == e.WhA)).OnHandQuantity);
    }

    [Fact]
    public async Task Return_MoreThanOutstanding_IsRejected_AndRepeatedReturnsAccumulateWithoutExceedingTheIssue()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(Stock(e, e.WhA, null, 0));
        await e.Db.SaveChangesAsync();
        var usageId = await IssueAsync(e, e.Wo1, e.WhA, null, 5);

        Assert.True((await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 3, null, null)).Success);
        var over = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 3, null, null);
        Assert.False(over.Success);
        Assert.Contains("2", over.Error);
        Assert.True((await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 2, null, null)).Success);

        e.Db.ChangeTracker.Clear();
        Assert.Equal(5m, (await e.Db.MaterialStocks.SingleAsync()).OnHandQuantity);
        Assert.Equal(2, await e.Db.InventoryTransactions.CountAsync(t => t.TransactionType == "Return"));
        Assert.Equal(5m, (await e.Db.MaintenanceSpareUsages.SingleAsync()).ReturnedQuantity);
    }

    [Fact]
    public async Task Return_WhenTheOriginHasDuplicateStockRows_FailsSafely_UsageAndLedgerUntouched()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.AddRange(Stock(e, e.WhA, null, 1), Stock(e, e.WhA, null, 2));
        await e.Db.SaveChangesAsync();
        var usageId = await IssueAsync(e, e.Wo1, e.WhA, null, 4);

        var (ok, err, _) = await WorkOrderSpareReturn.ReturnAsync(e.Db, usageId, e.Wo1, 1, null, null);

        Assert.False(ok);
        Assert.Contains("MaterialStockId", err);
        e.Db.ChangeTracker.Clear();
        Assert.Null((await e.Db.MaintenanceSpareUsages.SingleAsync()).ReturnedQuantity);
        Assert.DoesNotContain(await e.Db.InventoryTransactions.ToListAsync(), t => t.TransactionType == "Return");
    }
}
