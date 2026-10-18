using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§H/§J، Phase 7.5): پیش از این
/// PurchaseOrder هیچ Controller/View/مجوزی نداشت (§H) - این تست‌ها ثابت می‌کنند گیت مجوز تازه
/// اضافه‌شده واقعاً کار می‌کند.
/// </summary>
public class PurchaseOrderControllerAuthorizationTests
{
    private static async Task<(PurchaseOrderController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"PoCtrlAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { UserId = 1, Roles = role != null ? new List<string> { role } : new List<string>() };
        var service = new PurchaseOrderService(ctx);
        var controller = new PurchaseOrderController(service, permissionService, currentUser, ctx, accessControl).WithTempData();

        return (controller, ctx);
    }

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbid_DoesNotCreatePO()
    {
        var (controller, ctx) = await CreateController(nameof(Create_WithoutCreatePermission_ReturnsForbid_DoesNotCreatePO), role: SystemRoles.Viewer);
        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        var vendor = new Vendor { Name = "Vendor A", IsActive = true };
        ctx.Projects.Add(project);
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var result = await controller.Create("PO-001", project.ProjectId, vendor.VendorId, null, null, 1000m);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.PurchaseOrders);
    }

    [Fact]
    public async Task Create_WithCreatePermission_CreatesPurchaseOrder()
    {
        var (controller, ctx) = await CreateController(nameof(Create_WithCreatePermission_CreatesPurchaseOrder), role: SystemRoles.ContractManager);
        var project = new Project { ProjectCode = "P1", ProjectName = "Project One", IsActive = true };
        var vendor = new Vendor { Name = "Vendor A", IsActive = true };
        ctx.Projects.Add(project);
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var result = await controller.Create("PO-001", project.ProjectId, vendor.VendorId, null, null, 1000m);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.PurchaseOrders);
    }
}
