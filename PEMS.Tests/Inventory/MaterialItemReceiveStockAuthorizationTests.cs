using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق ممیزی Material Management Step 1 (یافته امنیتی: MaterialItemController.ReceiveStock
/// هیچ بررسی مجوزی نداشت - هر کاربر واردشده می‌توانست مستقیماً OnHandQuantity را افزایش دهد،
/// و این مسیر می‌توانست کل گیت QC/Disposition جدید IRN را دور بزند) + Step 3 بخش ۱۴: این تست‌ها
/// ثابت می‌کنند رفع حداقلی واقعاً اعمال شده - نه فقط یک دکمه‌ی مخفی در UI.
/// </summary>
public class MaterialItemReceiveStockAuthorizationTests
{
    private static (MaterialItemController Controller, PEMSDbContext Ctx) CreateController(string dbName, bool allow)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"MatRecv_{dbName}");
        ctx.Warehouses.Add(new Warehouse { WarehouseId = 1, Code = "WH-01", Name = "Main", IsActive = true });
        ctx.MaterialItems.Add(new MaterialItem { MaterialItemId = 1, MaterialCode = "MAT-001", Description = "Widget", IsActive = true, IsStockItem = true });
        ctx.SaveChanges();

        var currentUser = new FakeCurrentUserService { UserId = 1, Roles = new[] { "MaintenanceEngineer" } };
        var perm = new FakePermissionService { AllowAll = allow };
        var stockService = new MaterialStockService(ctx);

        var controller = new MaterialItemController(ctx, null!, stockService, currentUser, perm, null!).WithTempData();
        return (controller, ctx);
    }

    [Fact]
    public async Task ReceiveStock_by_unauthorized_user_returns_Forbid_and_does_not_change_stock()
    {
        var (controller, ctx) = CreateController(nameof(ReceiveStock_by_unauthorized_user_returns_Forbid_and_does_not_change_stock), allow: false);

        var result = await controller.ReceiveStock(1, 1, 50, "unauthorized attempt");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await ctx.MaterialStocks.CountAsync());
        Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task ReceiveStock_by_authorized_user_succeeds()
    {
        var (controller, ctx) = CreateController(nameof(ReceiveStock_by_authorized_user_succeeds), allow: true);

        var result = await controller.ReceiveStock(1, 1, 50, "authorized receipt");

        Assert.IsType<RedirectToActionResult>(result);
        var stock = await ctx.MaterialStocks.SingleAsync();
        Assert.Equal(50, stock.OnHandQuantity);
    }
}
