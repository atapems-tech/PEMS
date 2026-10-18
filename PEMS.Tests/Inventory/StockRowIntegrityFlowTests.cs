using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P4 - رفتار مسیرهای IRN و MRS Import روی ردیف‌های موجودی (InMemory):
/// چند خط هم‌متریال در یک IRN فقط یک ردیف می‌سازد؛ ردیف‌های تکراری ⇒ توقف کامل بدون Post/تغییر وضعیت؛ اجرای مجدد Idempotent.
/// </summary>
public class StockRowIntegrityFlowTests
{
    // ───────── IRN ─────────

    private static (IrnReceiptService svc, PEMSDbContext ctx) SeedIrn(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P4Irn_{dbName}");
        ctx.Vendors.Add(new Vendor { VendorId = 1, Name = "Test Vendor", IsActive = true });
        ctx.Warehouses.Add(new Warehouse { WarehouseId = 1, Code = "WH-01", Name = "Main", IsActive = true });
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Warehouse" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Receiving", LastName = "Clerk", DepartmentId = 1, IsActive = true });
        ctx.Employees.Add(new Employee { EmployeeId = 2, FirstName = "QA", LastName = "Manager", DepartmentId = 1, IsActive = true });
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 1, MaterialCode = "MAT-001", Description = "Widget", IsActive = true, IsStockItem = true });
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 2, MaterialCode = "MAT-002", Description = "Gadget", IsActive = true, IsStockItem = true });
        ctx.SaveChanges();
        return (new IrnReceiptService(ctx), ctx);
    }

    private static async Task<int> SeedSubmittedAsync(PEMSDbContext ctx, params (int Item, decimal Qty)[] lines)
    {
        var receipt = new IrnReceipt
        {
            IrnNumber = $"IRN-2026-{Guid.NewGuid().ToString("N")[..4]}", Status = "Submitted", VendorId = 1, WarehouseId = 1,
            ReceivedById = 1, ReceivingDate = DateTime.Now, IsActive = true, CreatedDate = DateTime.Now
        };
        foreach (var (item, qty) in lines)
            receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = item, ReceivedQuantity = qty, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();
        return receipt.IrnReceiptId;
    }

    private static async Task AcceptAllAsync(IrnReceiptService svc, PEMSDbContext ctx, int receiptId)
    {
        var lines = await ctx.IrnReceiptLines.Where(l => l.IrnReceiptId == receiptId).ToListAsync();
        var (ok, err, _) = await svc.RecordDispositionAsync(receiptId, lines.Select(l => (LineId: l.IrnReceiptLineId, Accepted: l.ReceivedQuantity, Rejected: 0m, Hold: 0m)).ToArray(), null, 2);
        Assert.True(ok, err);
    }

    [Fact]
    public async Task Irn_TwoLinesSameMaterial_CreateOneStockRow_WithBothQuantities_AndTwoTransactions()
    {
        var (svc, ctx) = SeedIrn(nameof(Irn_TwoLinesSameMaterial_CreateOneStockRow_WithBothQuantities_AndTwoTransactions));
        var receiptId = await SeedSubmittedAsync(ctx, (1, 5), (1, 8));
        await AcceptAllAsync(svc, ctx, receiptId);

        var (ok, err) = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.True(ok, err);
        var row = await ctx.MaterialStocks.SingleAsync(x => x.MaterialItemId == 1 && x.WarehouseId == 1);
        Assert.Equal(13m, row.OnHandQuantity);
        Assert.Equal(2, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceType == "IrnReceipt" && t.ReferenceId == receiptId));
    }

    [Fact]
    public async Task Irn_RepeatedCompletion_DoesNotDoublePost_AndKeepsOneRowAndOneTransactionPerLine()
    {
        var (svc, ctx) = SeedIrn(nameof(Irn_RepeatedCompletion_DoesNotDoublePost_AndKeepsOneRowAndOneTransactionPerLine));
        var receiptId = await SeedSubmittedAsync(ctx, (1, 5), (2, 8));
        await AcceptAllAsync(svc, ctx, receiptId);

        await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);
        await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.Equal(2, await ctx.MaterialStocks.CountAsync());
        Assert.Equal(5m, (await ctx.MaterialStocks.SingleAsync(x => x.MaterialItemId == 1)).OnHandQuantity);
        Assert.Equal(8m, (await ctx.MaterialStocks.SingleAsync(x => x.MaterialItemId == 2)).OnHandQuantity);
        Assert.Equal(2, await ctx.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task Irn_DuplicateNullLocationRows_StopTheWholeCompletion_NoPostingAndStatusUnchanged()
    {
        var (svc, ctx) = SeedIrn(nameof(Irn_DuplicateNullLocationRows_StopTheWholeCompletion_NoPostingAndStatusUnchanged));
        // مواد 2 سالم است ولی مواد 1 ردیف تکراری دارد: هیچ‌کدام نباید Post شود (Pre-resolve قبل از هر تغییر)
        ctx.MaterialStocks.AddRange(
            new MaterialStock { MaterialItemId = 1, WarehouseId = 1, OnHandQuantity = 3, IsActive = true },
            new MaterialStock { MaterialItemId = 1, WarehouseId = 1, OnHandQuantity = 4, IsActive = true });
        await ctx.SaveChangesAsync();
        var receiptId = await SeedSubmittedAsync(ctx, (2, 6), (1, 5));
        await AcceptAllAsync(svc, ctx, receiptId);
        var statusBefore = (await ctx.IrnReceipts.SingleAsync(r => r.IrnReceiptId == receiptId)).Status;

        var (ok, err) = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.False(ok);
        Assert.Contains("MaterialStockId", err);
        ctx.ChangeTracker.Clear();
        Assert.Equal(statusBefore, (await ctx.IrnReceipts.SingleAsync(r => r.IrnReceiptId == receiptId)).Status);
        Assert.Empty(await ctx.InventoryTransactions.ToListAsync());
        Assert.Empty(await ctx.MaterialStocks.Where(x => x.MaterialItemId == 2).ToListAsync());
        Assert.Equal(new[] { 3m, 4m }, (await ctx.MaterialStocks.Where(x => x.MaterialItemId == 1).OrderBy(x => x.MaterialStockId).Select(x => x.OnHandQuantity).ToListAsync()).ToArray());
    }

    // ───────── MRS Import ─────────

    private static MemoryStream Workbook(params string[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        var headers = new[] { "DESCRIPTION PACKING LIST", "MRS NO", "QTY RECEIVED" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++) ws.Cell(r + 2, c + 1).Value = rows[r][c];
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static async Task<(int BatchId, int RowId)> StageAsync(MrsImportService svc, PEMSDbContext ctx)
    {
        using var wb = Workbook(new[] { "P4 Gasket 4 inch", "WH-A-0026", "10" });
        var (ok, err, batchId) = await svc.StageFromExcelAsync(wb, "p4.xlsx", null, null);
        Assert.True(ok, err);
        var row = await ctx.MrsImportRows.SingleAsync(x => x.MrsImportBatchId == batchId);
        return (batchId!.Value, row.MrsImportRowId);
    }

    [Fact]
    public async Task MrsImport_SecondImportReusesTheSingleNullLocationRow()
    {
        var ctx = PemsDbContextFactory.CreateInMemory("P4Mrs_" + Guid.NewGuid().ToString("N"));
        ctx.Warehouses.Add(new Warehouse { Code = "WH-A", Name = "A", IsActive = true });
        await ctx.SaveChangesAsync();
        var svc = new MrsImportService(ctx, new VendorService(ctx));

        var first = await StageAsync(svc, ctx);
        var (ok1, e1, c1, _) = await svc.CommitAsync(first.BatchId, new List<int> { first.RowId }, null);
        Assert.True(ok1, e1); Assert.Equal(1, c1);
        var second = await StageAsync(svc, ctx);
        var (ok2, e2, c2, _) = await svc.CommitAsync(second.BatchId, new List<int> { second.RowId }, null);
        Assert.True(ok2, e2); Assert.Equal(1, c2);

        var stock = await ctx.MaterialStocks.SingleAsync();
        Assert.Equal(20m, stock.OnHandQuantity);
        Assert.Equal(2, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceType == "MrsImport"));
    }

    [Fact]
    public async Task MrsImport_DuplicateNullLocationRows_PostsNothing_AndReportsAmbiguityOnTheRow()
    {
        var ctx = PemsDbContextFactory.CreateInMemory("P4MrsDup_" + Guid.NewGuid().ToString("N"));
        ctx.Warehouses.Add(new Warehouse { Code = "WH-A", Name = "A", IsActive = true });
        await ctx.SaveChangesAsync();
        var svc = new MrsImportService(ctx, new VendorService(ctx));

        var first = await StageAsync(svc, ctx);
        var (ok1, e1, _, _) = await svc.CommitAsync(first.BatchId, new List<int> { first.RowId }, null);
        Assert.True(ok1, e1);
        var original = await ctx.MaterialStocks.SingleAsync();
        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = original.MaterialItemId, WarehouseId = original.WarehouseId, OnHandQuantity = 99, IsActive = true });
        await ctx.SaveChangesAsync();
        var txBefore = await ctx.InventoryTransactions.CountAsync();

        var second = await StageAsync(svc, ctx);
        await svc.CommitAsync(second.BatchId, new List<int> { second.RowId }, null);

        ctx.ChangeTracker.Clear();
        var row = await ctx.MrsImportRows.SingleAsync(r => r.MrsImportRowId == second.RowId);
        Assert.Contains("MaterialStockId", row.ErrorMessage);
        Assert.Equal(txBefore, await ctx.InventoryTransactions.CountAsync());
        Assert.Equal(new[] { 10m, 99m }, (await ctx.MaterialStocks.OrderBy(x => x.MaterialStockId).Select(x => x.OnHandQuantity).ToListAsync()).ToArray());
    }
}
