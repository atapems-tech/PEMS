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
/// طبق «PEMS Phase 7.5»: CmmsReadinessController یک صفحه‌ی گزارشیِ محض است (بدون Mutation) -
/// روی همان ماژول مجوز "MaintenanceMasterData" موجود سوار شده، دقیقاً مثل
/// EquipmentClassificationController.Coverage از فاز ۳.۵.
/// </summary>
public class CmmsReadinessControllerAuthorizationTests
{
    private static async Task<(CmmsReadinessController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"CmmsReadinessAuth_{dbName}");
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

        var readinessService = new EquipmentReadinessService(ctx, new EquipmentClassCoverageService(ctx));
        var controller = new CmmsReadinessController(readinessService, ctx, currentUser, permissionService).WithTempData();
        return (controller, ctx);
    }

    [Fact]
    public async Task Index_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ViewerRole_CanView()
    {
        var (controller, _) = await CreateController(nameof(Index_ViewerRole_CanView), role: SystemRoles.Viewer);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ClassDetail_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(ClassDetail_NoPermission_ReturnsForbid), role: null);

        var result = await controller.ClassDetail(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ClassDetail_UnknownClass_ReturnsNotFound_ForAuthorizedUser()
    {
        var (controller, _) = await CreateController(nameof(ClassDetail_UnknownClass_ReturnsNotFound_ForAuthorizedUser), role: SystemRoles.Viewer);

        var result = await controller.ClassDetail(99999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ClassDetail_KnownClass_ReturnsView_ForAuthorizedUser()
    {
        var (controller, ctx) = await CreateController(nameof(ClassDetail_KnownClass_ReturnsView_ForAuthorizedUser), role: SystemRoles.MaintenanceEngineer);
        var equipmentClass = new EquipmentClass { Code = "PUMP_TEST", Name = "Pump Test", IsActive = true };
        ctx.EquipmentClasses.Add(equipmentClass);
        await ctx.SaveChangesAsync();

        var result = await controller.ClassDetail(equipmentClass.EquipmentClassId);

        Assert.IsType<ViewResult>(result);
    }
}
