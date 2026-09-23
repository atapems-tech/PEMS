using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی Material Management (Gap B) + Step 3: اصل «Received ≠ Available». این تست‌ها
/// ثابت می‌کنند: (۱) ثبت Disposition فقط از Submitted مجاز است و جمع Accepted+Rejected+Hold
/// باید دقیقاً برابر مقدار دریافتی باشد، (۲) فقط بعد از CompleteDispositionAsync(approved:true)
/// - نه در لحظه‌ی ثبت Disposition - مقدار Accepted به InventoryTransaction/MaterialStock تبدیل
/// می‌شود، (۳) Rejected/Hold هرگز به هیچ Transaction ای تبدیل نمی‌شوند، (۴) Reject توسط QA خطوط
/// را برای بازبینی دوباره پاک می‌کند، (۵) فراخوانی دوم Idempotent است (بدون Post دوباره).
/// </summary>
public class IrnDispositionServiceTests
{
    private static (IrnReceiptService svc, PEMSDbContext ctx) Seed(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"IrnDisp_{dbName}");
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

    /// <summary>یک IRN با یک خط، در وضعیت Submitted، آماده‌ی Disposition.</summary>
    private static async Task<(int ReceiptId, int LineId)> SeedSubmittedReceiptAsync(PEMSDbContext ctx, decimal receivedQty = 10, int materialItemId = 1)
    {
        var receipt = new IrnReceipt
        {
            IrnNumber = $"IRN-2026-{Guid.NewGuid().ToString("N")[..4]}",
            Status = "Submitted",
            VendorId = 1,
            WarehouseId = 1,
            ReceivedById = 1,
            ReceivingDate = DateTime.Now,
            IsActive = true,
            CreatedDate = DateTime.Now
        };
        var line = new IrnReceiptLine { MaterialItemId = materialItemId, ReceivedQuantity = receivedQty, IsActive = true };
        receipt.Lines.Add(line);
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();
        return (receipt.IrnReceiptId, line.IrnReceiptLineId);
    }

    // ---------------- RecordDispositionAsync: positive ----------------
    [Fact]
    public async Task RecordDisposition_full_acceptance_succeeds_moves_to_PendingApproval_and_computes_Accepted_result()
    {
        var (svc, ctx) = Seed(nameof(RecordDisposition_full_acceptance_succeeds_moves_to_PendingApproval_and_computes_Accepted_result));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);

        var (success, error, recordId) = await svc.RecordDispositionAsync(
            receiptId, new[] { (LineId: lineId, Accepted: 10m, Rejected: 0m, Hold: 0m) }, "all good", 2);

        Assert.True(success, error);
        Assert.NotNull(recordId);

        var receipt = await ctx.IrnReceipts.Include(x => x.Lines).FirstAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal("PendingApproval", receipt.Status);
        Assert.Equal(10, receipt.Lines.Single().AcceptedQuantity);

        var record = await ctx.IrnDispositionRecords.SingleAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal("Accepted", record.OverallResult);
        Assert.Equal("Pending", record.WorkflowOutcome);
        Assert.Equal(2, record.DispositionedById);
    }

    [Theory]
    [InlineData(0, 10, 0, "Rejected")]
    [InlineData(0, 0, 10, "Hold")]
    [InlineData(4, 3, 3, "PartiallyAccepted")]
    public async Task RecordDisposition_computes_correct_overall_result(decimal accepted, decimal rejected, decimal hold, string expectedResult)
    {
        var (svc, ctx) = Seed(nameof(RecordDisposition_computes_correct_overall_result) + expectedResult);
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);

        var (success, error, _) = await svc.RecordDispositionAsync(
            receiptId, new[] { (LineId: lineId, Accepted: accepted, Rejected: rejected, Hold: hold) }, null, 2);

        Assert.True(success, error);
        var record = await ctx.IrnDispositionRecords.SingleAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal(expectedResult, record.OverallResult);
    }

    // ---------------- RecordDispositionAsync: negative / boundary ----------------
    [Fact]
    public async Task RecordDisposition_from_Draft_fails()
    {
        var (svc, ctx) = Seed(nameof(RecordDisposition_from_Draft_fails));
        var receipt = new IrnReceipt { IrnNumber = "IRN-2026-0001", Status = "Draft", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true };
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 10, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, error, _) = await svc.RecordDispositionAsync(
            receipt.IrnReceiptId, new[] { (LineId: receipt.Lines.First().IrnReceiptLineId, Accepted: 10m, Rejected: 0m, Hold: 0m) }, null, 2);

        Assert.False(success);
        Assert.Contains("Submitted", error);
    }

    [Fact]
    public async Task RecordDisposition_quantity_sum_not_matching_received_fails()
    {
        var (svc, ctx) = Seed(nameof(RecordDisposition_quantity_sum_not_matching_received_fails));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);

        var (success, error, _) = await svc.RecordDispositionAsync(
            receiptId, new[] { (LineId: lineId, Accepted: 8m, Rejected: 1m, Hold: 0m) }, null, 2); // sums to 9, not 10

        Assert.False(success);
        Assert.Contains("دریافتی", error);
        Assert.Equal("Submitted", (await ctx.IrnReceipts.FirstAsync(x => x.IrnReceiptId == receiptId)).Status);
    }

    [Fact]
    public async Task RecordDisposition_negative_quantity_fails()
    {
        var (svc, ctx) = Seed(nameof(RecordDisposition_negative_quantity_fails));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);

        var (success, _, _) = await svc.RecordDispositionAsync(
            receiptId, new[] { (LineId: lineId, Accepted: -1m, Rejected: 11m, Hold: 0m) }, null, 2);

        Assert.False(success);
    }

    [Fact]
    public async Task RecordDisposition_missing_a_line_fails()
    {
        var (svc, ctx) = Seed(nameof(RecordDisposition_missing_a_line_fails));
        var receipt = new IrnReceipt { IrnNumber = "IRN-2026-0002", Status = "Submitted", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true };
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 10, IsActive = true });
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 2, ReceivedQuantity = 5, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        // فقط یکی از دو خط Disposition می‌شود - باید رد شود.
        var (success, error, _) = await svc.RecordDispositionAsync(
            receipt.IrnReceiptId, new[] { (LineId: receipt.Lines.First().IrnReceiptLineId, Accepted: 10m, Rejected: 0m, Hold: 0m) }, null, 2);

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ---------------- CompleteDispositionAsync: the core Received ≠ Available proof ----------------
    [Fact]
    public async Task CompleteDisposition_approved_posts_only_accepted_quantity_as_an_Acceptance_transaction()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_approved_posts_only_accepted_quantity_as_an_Acceptance_transaction));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);
        await svc.RecordDispositionAsync(receiptId, new[] { (LineId: lineId, Accepted: 7m, Rejected: 2m, Hold: 1m) }, null, 2);

        var (success, error) = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.True(success, error);

        var stock = await ctx.MaterialStocks.SingleAsync(x => x.MaterialItemId == 1 && x.WarehouseId == 1);
        Assert.Equal(7, stock.OnHandQuantity); // فقط Accepted - نه ۱۰ (کل دریافتی) و نه ۸ (Accepted+Hold)

        var tx = await ctx.InventoryTransactions.SingleAsync();
        Assert.Equal("Acceptance", tx.TransactionType);
        Assert.Equal(7, tx.Quantity);
        Assert.Equal("IrnReceipt", tx.ReferenceType);
        Assert.Equal(receiptId, tx.ReferenceId);

        var receipt = await ctx.IrnReceipts.FirstAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal("Closed", receipt.Status);

        var record = await ctx.IrnDispositionRecords.SingleAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal("Approved", record.WorkflowOutcome);
    }

    [Fact]
    public async Task CompleteDisposition_full_rejection_posts_no_stock_and_creates_no_transaction()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_full_rejection_posts_no_stock_and_creates_no_transaction));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);
        await svc.RecordDispositionAsync(receiptId, new[] { (LineId: lineId, Accepted: 0m, Rejected: 10m, Hold: 0m) }, null, 2);

        var (success, error) = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.True(success, error);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
        Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task CompleteDisposition_full_hold_posts_no_stock_and_creates_no_transaction()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_full_hold_posts_no_stock_and_creates_no_transaction));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);
        await svc.RecordDispositionAsync(receiptId, new[] { (LineId: lineId, Accepted: 0m, Rejected: 0m, Hold: 10m) }, null, 2);

        var (success, error) = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.True(success, error);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
        Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task CompleteDisposition_rejected_by_QA_resets_lines_and_returns_to_Submitted_for_reinspection()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_rejected_by_QA_resets_lines_and_returns_to_Submitted_for_reinspection));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);
        await svc.RecordDispositionAsync(receiptId, new[] { (LineId: lineId, Accepted: 10m, Rejected: 0m, Hold: 0m) }, null, 2);

        var (success, error) = await svc.CompleteDispositionAsync(receiptId, approved: false, actorEmployeeId: 2);

        Assert.True(success, error);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
        Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());

        var receipt = await ctx.IrnReceipts.Include(x => x.Lines).FirstAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal("Submitted", receipt.Status);
        Assert.Null(receipt.Lines.Single().AcceptedQuantity);

        var record = await ctx.IrnDispositionRecords.SingleAsync(x => x.IrnReceiptId == receiptId);
        Assert.Equal("Rejected", record.WorkflowOutcome);
    }

    // ---------------- Idempotency: retrying an already-completed disposition must not double-post ----------------
    [Fact]
    public async Task CompleteDisposition_called_twice_does_not_double_post_stock()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_called_twice_does_not_double_post_stock));
        var (receiptId, lineId) = await SeedSubmittedReceiptAsync(ctx, 10);
        await svc.RecordDispositionAsync(receiptId, new[] { (LineId: lineId, Accepted: 10m, Rejected: 0m, Hold: 0m) }, null, 2);

        var first = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);
        Assert.True(first.Success, first.Error);

        var second = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);
        Assert.False(second.Success);

        Assert.Equal(10, (await ctx.MaterialStocks.SingleAsync()).OnHandQuantity);
        Assert.Equal(1, await ctx.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task CompleteDisposition_when_not_PendingApproval_fails_cleanly()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_when_not_PendingApproval_fails_cleanly));
        var (receiptId, _) = await SeedSubmittedReceiptAsync(ctx, 10);

        var (success, error) = await svc.CompleteDispositionAsync(receiptId, approved: true, actorEmployeeId: 2);

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ---------------- Multiple materials into different warehouses/stock rows ----------------
    [Fact]
    public async Task CompleteDisposition_with_multiple_lines_posts_each_to_its_own_stock_row()
    {
        var (svc, ctx) = Seed(nameof(CompleteDisposition_with_multiple_lines_posts_each_to_its_own_stock_row));
        var receipt = new IrnReceipt { IrnNumber = "IRN-2026-0003", Status = "Submitted", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true };
        var line1 = new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 5, IsActive = true };
        var line2 = new IrnReceiptLine { MaterialItemId = 2, ReceivedQuantity = 8, IsActive = true };
        receipt.Lines.Add(line1);
        receipt.Lines.Add(line2);
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        await svc.RecordDispositionAsync(receipt.IrnReceiptId, new[]
        {
            (LineId: line1.IrnReceiptLineId, Accepted: 5m, Rejected: 0m, Hold: 0m),
            (LineId: line2.IrnReceiptLineId, Accepted: 6m, Rejected: 2m, Hold: 0m)
        }, null, 2);

        var (success, error) = await svc.CompleteDispositionAsync(receipt.IrnReceiptId, approved: true, actorEmployeeId: 2);
        Assert.True(success, error);

        Assert.Equal(5, (await ctx.MaterialStocks.SingleAsync(x => x.MaterialItemId == 1)).OnHandQuantity);
        Assert.Equal(6, (await ctx.MaterialStocks.SingleAsync(x => x.MaterialItemId == 2)).OnHandQuantity);
        Assert.Equal(2, await ctx.InventoryTransactions.CountAsync());
    }
}
