using PEMS.Web.Areas.Warehouse.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق Step 2 بخش ۱۵ («Controller / Service Security»): همه‌ی عملیات IRN باید در مرز
/// Controller/Service مجوز را اعمال کنند - نه فقط با مخفی‌کردن دکمه در UI. این تست‌ها ثابت
/// می‌کنند دسترسی مستقیم HTTP بدون مجوز واقعاً شکست می‌خورد (Forbid)، نه این‌که فقط UI دکمه
/// را نشان ندهد.
/// </summary>
public class IrnReceiptControllerTests
{
    private sealed class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null)
            => Task.FromResult($"{documentType}-TEST-{++_counter:D4}");
    }

    private static (IrnReceiptController Controller, PEMSDbContext Ctx, FakePermissionService Perm, FakeWorkflowRuntime Runtime) CreateController(string dbName, bool allow = true)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"IrnCtrl_{dbName}");
        ctx.Vendors.Add(new Vendor { VendorId = 1, Name = "Test Vendor", IsActive = true });
        ctx.Warehouses.Add(new Warehouse { WarehouseId = 1, Code = "WH-01", Name = "Main", IsActive = true });
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Warehouse" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Receiving", LastName = "Clerk", DepartmentId = 1, IsActive = true });
        ctx.Users.Add(new ApplicationUser { Id = 1, EmployeeId = 1, UserName = "clerk" });
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 1, MaterialCode = "MAT-001", Description = "Test Material", IsActive = true, IsStockItem = true });
        ctx.SaveChanges();

        var currentUser = new FakeCurrentUserService { UserId = 1, Roles = new[] { "MaintenanceEngineer" } };
        var perm = new FakePermissionService { AllowAll = allow };
        var lifecycle = new IrnReceiptService(ctx);
        var runtime = new FakeWorkflowRuntime();
        var controller = new IrnReceiptController(ctx, lifecycle, currentUser, perm, new FakeNumberingService(), runtime).WithTempData();
        return (controller, ctx, perm, runtime);
    }

    // ---------------- Authorization: unauthorized user cannot create/submit/cancel/add-line ----------------
    [Fact]
    public async Task Create_by_unauthorized_user_returns_Forbid_and_creates_nothing()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Create_by_unauthorized_user_returns_Forbid_and_creates_nothing), allow: false);

        var result = await controller.Create(1, 1, DateTime.Now, null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await ctx.IrnReceipts.CountAsync());
    }

    [Fact]
    public async Task Submit_by_unauthorized_user_returns_Forbid_and_does_not_change_status()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Submit_by_unauthorized_user_returns_Forbid_and_does_not_change_status), allow: false);
        ctx.IrnReceipts.Add(new IrnReceipt { IrnReceiptId = 1, IrnNumber = "IRN-2026-0001", Status = "Draft", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.Submit(1, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Draft", (await ctx.IrnReceipts.FirstAsync()).Status);
    }

    [Fact]
    public async Task Cancel_by_unauthorized_user_returns_Forbid()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Cancel_by_unauthorized_user_returns_Forbid), allow: false);
        ctx.IrnReceipts.Add(new IrnReceipt { IrnReceiptId = 1, IrnNumber = "IRN-2026-0001", Status = "Draft", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.Cancel(1, null);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AddLine_by_unauthorized_user_returns_Forbid_and_adds_no_line()
    {
        var (controller, ctx, _, _) = CreateController(nameof(AddLine_by_unauthorized_user_returns_Forbid_and_adds_no_line), allow: false);
        ctx.IrnReceipts.Add(new IrnReceipt { IrnReceiptId = 1, IrnNumber = "IRN-2026-0001", Status = "Draft", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.AddLine(1, 1, 5);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await ctx.IrnReceiptLines.CountAsync());
    }

    // ---------------- Positive: authorized create succeeds and assigns a number via INumberingService ----------------
    [Fact]
    public async Task Create_by_authorized_user_succeeds_and_assigns_a_number()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Create_by_authorized_user_succeeds_and_assigns_a_number));

        var result = await controller.Create(1, 1, DateTime.Now, null, "first receipt");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var receipt = await ctx.IrnReceipts.SingleAsync();
        Assert.Equal("Draft", receipt.Status);
        Assert.StartsWith("IRN-TEST-", receipt.IrnNumber);
        Assert.Equal(1, receipt.ReceivedById);
    }

    // ---------------- Boundary: invalid FK inputs fail cleanly, not with an unhandled exception ----------------
    [Fact]
    public async Task Create_with_a_nonexistent_vendor_fails_cleanly()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Create_with_a_nonexistent_vendor_fails_cleanly));

        var result = await controller.Create(999, 1, DateTime.Now, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, await ctx.IrnReceipts.CountAsync());
    }

    [Fact]
    public async Task AddLine_with_a_nonexistent_material_fails_cleanly()
    {
        var (controller, ctx, _, _) = CreateController(nameof(AddLine_with_a_nonexistent_material_fails_cleanly));
        ctx.IrnReceipts.Add(new IrnReceipt { IrnReceiptId = 1, IrnNumber = "IRN-2026-0001", Status = "Draft", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.AddLine(1, 999, 5);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, await ctx.IrnReceiptLines.CountAsync());
    }

    [Fact]
    public async Task AddLine_to_a_submitted_receipt_is_rejected_no_silent_edit()
    {
        var (controller, ctx, _, _) = CreateController(nameof(AddLine_to_a_submitted_receipt_is_rejected_no_silent_edit));
        ctx.IrnReceipts.Add(new IrnReceipt { IrnReceiptId = 1, IrnNumber = "IRN-2026-0001", Status = "Submitted", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.AddLine(1, 1, 5);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, await ctx.IrnReceiptLines.CountAsync());
    }

    // ---------------- Step 3: Disposition / Approve wiring + authorization ----------------
    private static async Task<int> SeedSubmittedWithLineAsync(PEMSDbContext ctx, decimal qty = 10)
    {
        var receipt = new IrnReceipt { IrnNumber = "IRN-2026-0099", Status = "Submitted", VendorId = 1, WarehouseId = 1, ReceivedById = 1, IsActive = true };
        receipt.Lines.Add(new IrnReceiptLine { MaterialItemId = 1, ReceivedQuantity = qty, IsActive = true });
        ctx.IrnReceipts.Add(receipt);
        await ctx.SaveChangesAsync();
        return receipt.IrnReceiptId;
    }

    [Fact]
    public async Task Disposition_by_unauthorized_user_returns_Forbid_and_does_not_start_a_workflow()
    {
        var (controller, ctx, _, runtime) = CreateController(nameof(Disposition_by_unauthorized_user_returns_Forbid_and_does_not_start_a_workflow), allow: false);
        var receiptId = await SeedSubmittedWithLineAsync(ctx);
        var lineId = (await ctx.IrnReceiptLines.FirstAsync()).IrnReceiptLineId;

        var result = await controller.Disposition(receiptId, new[] { lineId }, new[] { 10m }, new[] { 0m }, new[] { 0m }, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Submitted", (await ctx.IrnReceipts.FirstAsync()).Status);
        Assert.Null(await runtime.GetInstanceAsync("IrnReceiptDisposition", receiptId));
    }

    [Fact]
    public async Task Disposition_by_authorized_user_records_quantities_and_starts_the_approval_workflow()
    {
        var (controller, ctx, _, runtime) = CreateController(nameof(Disposition_by_authorized_user_records_quantities_and_starts_the_approval_workflow));
        var receiptId = await SeedSubmittedWithLineAsync(ctx);
        var lineId = (await ctx.IrnReceiptLines.FirstAsync()).IrnReceiptLineId;

        var result = await controller.Disposition(receiptId, new[] { lineId }, new[] { 7m }, new[] { 3m }, new[] { 0m }, "inspected");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("PendingApproval", (await ctx.IrnReceipts.FirstAsync()).Status);

        var instance = await runtime.GetInstanceAsync("IrnReceiptDisposition", receiptId);
        Assert.NotNull(instance);
        Assert.Equal("Running", instance!.Status);
    }

    [Fact]
    public async Task Approve_by_unauthorized_user_returns_Forbid_and_posts_no_stock()
    {
        var (controller, ctx, perm, runtime) = CreateController(nameof(Approve_by_unauthorized_user_returns_Forbid_and_posts_no_stock));
        var receiptId = await SeedSubmittedWithLineAsync(ctx);
        var lineId = (await ctx.IrnReceiptLines.FirstAsync()).IrnReceiptLineId;
        await controller.Disposition(receiptId, new[] { lineId }, new[] { 10m }, new[] { 0m }, new[] { 0m }, null);

        perm.AllowAll = false;
        var result = await controller.Approve(receiptId, "Approve", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
    }

    [Fact]
    public async Task Approve_with_decision_Approve_posts_accepted_stock_and_closes_the_IRN()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Approve_with_decision_Approve_posts_accepted_stock_and_closes_the_IRN));
        var receiptId = await SeedSubmittedWithLineAsync(ctx);
        var lineId = (await ctx.IrnReceiptLines.FirstAsync()).IrnReceiptLineId;
        await controller.Disposition(receiptId, new[] { lineId }, new[] { 10m }, new[] { 0m }, new[] { 0m }, null);

        var result = await controller.Approve(receiptId, "Approve", "looks good");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Closed", (await ctx.IrnReceipts.FirstAsync()).Status);
        Assert.Equal(10, (await ctx.MaterialStocks.SingleAsync()).OnHandQuantity);
    }

    [Fact]
    public async Task Approve_with_decision_Reject_posts_no_stock_and_returns_IRN_to_Submitted()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Approve_with_decision_Reject_posts_no_stock_and_returns_IRN_to_Submitted));
        var receiptId = await SeedSubmittedWithLineAsync(ctx);
        var lineId = (await ctx.IrnReceiptLines.FirstAsync()).IrnReceiptLineId;
        await controller.Disposition(receiptId, new[] { lineId }, new[] { 10m }, new[] { 0m }, new[] { 0m }, null);

        var result = await controller.Approve(receiptId, "Reject", "insufficient evidence");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Submitted", (await ctx.IrnReceipts.FirstAsync()).Status);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
    }

    [Fact]
    public async Task Approve_when_not_PendingApproval_fails_cleanly()
    {
        var (controller, ctx, _, _) = CreateController(nameof(Approve_when_not_PendingApproval_fails_cleanly));
        var receiptId = await SeedSubmittedWithLineAsync(ctx); // still Submitted, no disposition recorded yet

        var result = await controller.Approve(receiptId, "Approve", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Submitted", (await ctx.IrnReceipts.FirstAsync()).Status);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
    }
}
