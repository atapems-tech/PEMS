using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی Material Management (Gap A) + «Step 1 Closure Report» + Step 2: چرخه‌ی عمر IRN
/// (Draft→Submitted، +Cancelled). طبق مرز صریح Step 2، این تست‌ها صراحتاً ثابت می‌کنند
/// Submit/Cancel هیچ InventoryTransaction/MaterialStock ای نمی‌سازند یا تغییر نمی‌دهند - گیت
/// QC/Disposition و تعیین Available Stock عمداً به Step 3 موکول شده است.
/// </summary>
public class IrnReceiptServiceTests
{
    private static (IrnReceiptService svc, PEMSDbContext ctx) Seed(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"IrnSvc_{dbName}");
        ctx.Vendors.Add(new Vendor { VendorId = 1, Name = "Test Vendor", IsActive = true });
        ctx.Warehouses.Add(new Warehouse { WarehouseId = 1, Code = "WH-01", Name = "Main", IsActive = true });
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Warehouse" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Receiving", LastName = "Clerk", DepartmentId = 1, IsActive = true });
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 1, MaterialCode = "MAT-001", Description = "Test Material", IsActive = true, IsStockItem = true });
        ctx.SaveChanges();
        return (new IrnReceiptService(ctx), ctx);
    }

    private static IrnReceipt NewDraftReceipt(int id, string number = "IRN-2026-0001") => new()
    {
        IrnReceiptId = id,
        IrnNumber = number,
        Status = "Draft",
        VendorId = 1,
        WarehouseId = 1,
        ReceivedById = 1,
        ReceivingDate = DateTime.Now,
        IsActive = true,
        CreatedDate = DateTime.Now
    };

    // ---------------- Positive: valid submit ----------------
    [Fact]
    public async Task Submit_draft_with_a_positive_quantity_line_succeeds_and_records_history()
    {
        var (svc, ctx) = Seed(nameof(Submit_draft_with_a_positive_quantity_line_succeeds_and_records_history));
        var receipt = NewDraftReceipt(1);
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 10, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, error) = await svc.SubmitAsync(1, "ready for QC", 1);

        Assert.True(success, error);
        var reloaded = await ctx.IrnReceipts.FirstAsync(x => x.IrnReceiptId == 1);
        Assert.Equal("Submitted", reloaded.Status);

        var history = await ctx.IrnReceiptStatusHistories.SingleAsync(x => x.IrnReceiptId == 1);
        Assert.Equal("Draft", history.FromStatus);
        Assert.Equal("Submitted", history.ToStatus);
        Assert.Equal(1, history.ChangedById);
        Assert.Equal("ready for QC", history.Reason);
    }

    // ---------------- Negative: invalid state transition ----------------
    [Fact]
    public async Task Submit_a_batch_that_is_not_Draft_fails_with_a_clear_error()
    {
        var (svc, ctx) = Seed(nameof(Submit_a_batch_that_is_not_Draft_fails_with_a_clear_error));
        var receipt = NewDraftReceipt(1);
        receipt.Status = "Submitted";
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 5, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, error) = await svc.SubmitAsync(1, null, 1);

        Assert.False(success);
        Assert.Contains("Draft", error);
        Assert.Equal("Submitted", (await ctx.IrnReceipts.FirstAsync()).Status);
    }

    [Fact]
    public async Task Submit_a_draft_with_no_lines_fails()
    {
        var (svc, ctx) = Seed(nameof(Submit_a_draft_with_no_lines_fails));
        ctx.IrnReceipts.Add(NewDraftReceipt(1));
        await ctx.SaveChangesAsync();

        var (success, error) = await svc.SubmitAsync(1, null, 1);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Draft", (await ctx.IrnReceipts.FirstAsync()).Status);
    }

    [Fact]
    public async Task Submit_a_draft_whose_only_line_has_zero_quantity_fails()
    {
        var (svc, ctx) = Seed(nameof(Submit_a_draft_whose_only_line_has_zero_quantity_fails));
        var receipt = NewDraftReceipt(1);
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 0, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, _) = await svc.SubmitAsync(1, null, 1);

        Assert.False(success);
    }

    [Fact]
    public async Task Submitting_an_already_cancelled_receipt_fails()
    {
        var (svc, ctx) = Seed(nameof(Submitting_an_already_cancelled_receipt_fails));
        var receipt = NewDraftReceipt(1);
        receipt.Status = "Cancelled";
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 5, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, error) = await svc.SubmitAsync(1, null, 1);
        Assert.False(success);
    }

    // ---------------- Idempotency: submitting twice does not duplicate history or silently re-succeed ----------------
    [Fact]
    public async Task Submitting_a_receipt_twice_fails_the_second_time_and_does_not_duplicate_history()
    {
        var (svc, ctx) = Seed(nameof(Submitting_a_receipt_twice_fails_the_second_time_and_does_not_duplicate_history));
        var receipt = NewDraftReceipt(1);
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 10, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var first = await svc.SubmitAsync(1, null, 1);
        Assert.True(first.Success, first.Error);

        var second = await svc.SubmitAsync(1, null, 1);
        Assert.False(second.Success);

        Assert.Equal(1, await ctx.IrnReceiptStatusHistories.CountAsync(x => x.IrnReceiptId == 1));
    }

    // ---------------- Cancel: positive from Draft and Submitted, negative from Cancelled ----------------
    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    public async Task Cancel_succeeds_from_Draft_or_Submitted(string fromStatus)
    {
        var (svc, ctx) = Seed(nameof(Cancel_succeeds_from_Draft_or_Submitted) + fromStatus);
        var receipt = NewDraftReceipt(1);
        receipt.Status = fromStatus;
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, error) = await svc.CancelAsync(1, "no longer needed", 1);

        Assert.True(success, error);
        Assert.Equal("Cancelled", (await ctx.IrnReceipts.FirstAsync()).Status);
    }

    [Fact]
    public async Task Cancel_an_already_cancelled_receipt_fails()
    {
        var (svc, ctx) = Seed(nameof(Cancel_an_already_cancelled_receipt_fails));
        var receipt = NewDraftReceipt(1);
        receipt.Status = "Cancelled";
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        var (success, _) = await svc.CancelAsync(1, null, 1);
        Assert.False(success);
    }

    // ---------------- The Step 2 boundary itself: Submit/Cancel must never touch InventoryTransaction/MaterialStock ----------------
    [Fact]
    public async Task Submitting_or_cancelling_an_IRN_creates_no_InventoryTransaction_and_changes_no_MaterialStock()
    {
        var (svc, ctx) = Seed(nameof(Submitting_or_cancelling_an_IRN_creates_no_InventoryTransaction_and_changes_no_MaterialStock));
        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = 1, WarehouseId = 1, OnHandQuantity = 5, IsActive = true });
        var receipt = NewDraftReceipt(1);
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = 10, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();

        await svc.SubmitAsync(1, null, 1);

        Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
        Assert.Equal(5, (await ctx.MaterialStocks.FirstAsync()).OnHandQuantity);

        await svc.CancelAsync(1, null, 1);

        Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
        Assert.Equal(5, (await ctx.MaterialStocks.FirstAsync()).OnHandQuantity);
    }
}
