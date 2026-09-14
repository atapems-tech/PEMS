using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS CMMS — Data Migration &amp; Controlled Data Onboarding» بند «Authorization»: همون
/// الگوی EquipmentFunctionalLocationAssignmentControllerAuthorizationTests - Upload/Review/
/// ToggleRow با Create/View مناسب، Commit (پرریسک‌ترین - رکورد واقعی درج می‌کند) با Approve،
/// Discard با Delete. اثبات می‌کند کاربر نمی‌تواند با درخواست مستقیم از مرحله‌ی تایید عبور کند.
/// </summary>
public class MaintenanceHistoryImportControllerAuthorizationTests
{
    private static async Task<(MaintenanceHistoryImportController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"MhImportAuth_{dbName}");
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
        var importService = new MaintenanceHistoryImportService(ctx);

        var controller = new MaintenanceHistoryImportController(importService, ctx, currentUser, permissionService).WithTempData();
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
    public async Task Upload_ViewerRole_CannotUpload_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Upload_ViewerRole_CannotUpload_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Upload(null!);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Review_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Review_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Review(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ToggleRow_ViewerRole_CannotToggle_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(ToggleRow_ViewerRole_CannotToggle_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.ToggleRow(1, true, 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Commit_QAManagerWithoutModulePermission_Forbidden()
    {
        // طبق سند مستر: Commit پرریسک‌ترین اکشن است (رکورد MaintenanceHistory واقعی درج می‌کند)
        // و باید Approve بخواهد - یک نقشی که اصلاً روی این ماژول مجوزی ندارد برای اثبات گیت واقعی.
        var (controller, _) = await CreateController(nameof(Commit_QAManagerWithoutModulePermission_Forbidden), role: SystemRoles.QAManager);

        var result = await controller.Commit(99999);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Commit_MaintenanceEngineer_HasApprove_GateAllowsThrough()
    {
        var (controller, _) = await CreateController(nameof(Commit_MaintenanceEngineer_HasApprove_GateAllowsThrough), role: SystemRoles.MaintenanceEngineer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Commit(99999));
    }

    [Fact]
    public async Task Discard_ViewerRole_CannotDiscard_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Discard_ViewerRole_CannotDiscard_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Discard(99999);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Discard_EngineeringManager_GateAllowsThrough()
    {
        var (controller, _) = await CreateController(nameof(Discard_EngineeringManager_GateAllowsThrough), role: SystemRoles.EngineeringManager);

        var result = await controller.Discard(99999);

        Assert.IsType<RedirectToActionResult>(result);
    }
}
