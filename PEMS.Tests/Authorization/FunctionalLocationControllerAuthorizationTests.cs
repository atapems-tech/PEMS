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

    // SCRUM-700: the list is a read operation (FunctionalLocation.View, checked by the host); the TreeNodes endpoint of the picker stays in the controller
    private static string ViewPermission() => ((PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IAssetPagesOperations).GetMethod("LocationListAsync")!
        .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!;

    [Fact]
    public async Task List_WithoutViewPermission_IsNotAllowed()
    {
        var (_, ctx) = await CreateController(nameof(List_WithoutViewPermission_IsNotAllowed), role: null);
        var parts = ViewPermission().Split('.');
        Assert.False(await new PermissionService(ctx, new AccessControlService(ctx)).HasPermissionAsync(new List<string>(), parts[0], parts[1]));
    }

    [Fact]
    public async Task List_WithViewPermission_ReturnsTheRows_AndValidateRefusesACycle()
    {
        var (_, ctx) = await CreateController(nameof(List_WithViewPermission_ReturnsTheRows_AndValidateRefusesACycle), role: SystemRoles.Viewer);
        var parts = ViewPermission().Split('.');
        Assert.True(await new PermissionService(ctx, new AccessControlService(ctx)).HasPermissionAsync(new List<string> { SystemRoles.Viewer }, parts[0], parts[1]));
        var parent = new PEMS.Domain.Entities.FunctionalLocation { Code = "P", Name = "Parent", IsActive = true, CreatedDate = DateTime.Now };
        ctx.FunctionalLocations.Add(parent);
        await ctx.SaveChangesAsync();
        var child = new PEMS.Domain.Entities.FunctionalLocation { Code = "C", Name = "Child", ParentId = parent.FunctionalLocationId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.FunctionalLocations.Add(child);
        await ctx.SaveChangesAsync();
        var ops = new PEMS.Web.Services.AssetPagesOperations(ctx, null!, new FunctionalLocationService(ctx));
        Assert.Equal(2, (await ops.LocationListAsync()).Count);
        Assert.False((await ops.ValidateLocationAsync(parent.FunctionalLocationId, "P", child.FunctionalLocationId)).Success);   // the parent under its own child
        Assert.False((await ops.ValidateLocationAsync(child.FunctionalLocationId, "P")).Success);                                  // a code already used
        Assert.True((await ops.ValidateLocationAsync(child.FunctionalLocationId, "C", parent.FunctionalLocationId)).Success);
    }

    // Create and Edit are the operations FunctionalLocation.Create / Edit (permissions FunctionalLocation.Create / Edit, SCRUM-700); the operation host refuses a role without them.
    private static (PEMS.Web.Services.AssetRegisterOperations Ops, PEMS.Persistence.Context.PEMSDbContext Ctx) Operations(string name)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"FLOps_{name}");
        var ops = new PEMS.Web.Services.AssetRegisterOperations(null!, new FunctionalLocationService(ctx), null!, new FakeCurrentUserService { UserName = "tester" });
        return (ops, ctx);
    }

    [Fact]
    public async Task CreateOperation_CreatesFunctionalLocation()
    {
        var (ops, ctx) = Operations(nameof(CreateOperation_CreatesFunctionalLocation));

        var result = await ops.CreateLocationAsync("FL-001", "Test");

        Assert.True(result.Success, result.Error);
        Assert.Single(ctx.FunctionalLocations);
    }

    [Fact]
    public async Task EditOperation_UpdatesFunctionalLocation()
    {
        var (ops, ctx) = Operations(nameof(EditOperation_UpdatesFunctionalLocation));
        var fl = new FunctionalLocation { FunctionalLocationId = 1, Code = "FL-001", Name = "Test", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var result = await ops.EditLocationAsync(fl.FunctionalLocationId, "FL-001", "Renamed");

        Assert.True(result.Success, result.Error);
        Assert.Equal("Renamed", ctx.FunctionalLocations.Single().Name);
    }
}
