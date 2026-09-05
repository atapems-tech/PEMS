using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 6B Wave A — ممیزی مستقل» بند P0: FunctionalLocationController.Index/Create/
/// Edit قبلاً هیچ بررسی مجوزی نداشتند.
/// </summary>
public class FunctionalLocationControllerAuthorizationTests
{
    private static async Task<(FunctionalLocationController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.EngineeringManager)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"FLAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };

        var flService = new FunctionalLocationService(ctx);
        var controller = new FunctionalLocationController(flService, currentUser, permissionService).WithTempData();
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
    public async Task Index_WithViewPermission_ReturnsView()
    {
        var (controller, _) = await CreateController(nameof(Index_WithViewPermission_ReturnsView), role: SystemRoles.Viewer);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<PagedResult<FunctionalLocationListDto>>(view.Model);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Create_WithoutCreatePermission_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Create("FL-001", "Test", null, null, null, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_WithCreatePermission_CreatesFunctionalLocation()
    {
        var (controller, ctx) = await CreateController(nameof(Create_WithCreatePermission_CreatesFunctionalLocation));

        var result = await controller.Create("FL-001", "Test", null, null, null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.FunctionalLocations);
    }

    [Fact]
    public async Task Edit_WithoutEditPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Edit_WithoutEditPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var fl = new FunctionalLocation { FunctionalLocationId = 1, Code = "FL-001", Name = "Test", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var result = await controller.Edit(fl.FunctionalLocationId, "FL-001", "Renamed", null, null, null, null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Test", ctx.FunctionalLocations.Single().Name);
    }

    [Fact]
    public async Task Edit_WithEditPermission_UpdatesFunctionalLocation()
    {
        var (controller, ctx) = await CreateController(nameof(Edit_WithEditPermission_UpdatesFunctionalLocation));
        var fl = new FunctionalLocation { FunctionalLocationId = 1, Code = "FL-001", Name = "Test", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var result = await controller.Edit(fl.FunctionalLocationId, "FL-001", "Renamed", null, null, null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Renamed", ctx.FunctionalLocations.Single().Name);
    }
}
