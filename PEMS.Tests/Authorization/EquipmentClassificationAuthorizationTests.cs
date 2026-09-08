using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification» بند ۴۲: این Controller هیچ ماژول مجوز جدیدی
/// نساخته - از همان "MaintenanceMasterData" موجود استفاده می‌کند؛ این تست‌ها اثبات می‌کنند گیت
/// واقعاً روی همون مجوز است (نه بدون بررسی باز مانده باشد).
/// </summary>
public class EquipmentClassificationAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"EquipClassAuth_{dbName}");
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
        return (ctx, permissionService, currentUser);
    }

    private class FakeClassificationService : IEquipmentClassificationService
    {
        public Task<EquipmentClassificationReportDto> GetClassificationReportAsync() => Task.FromResult(new EquipmentClassificationReportDto());
        public Task<int> BackfillAsync() => Task.FromResult(0);
    }

    [Fact]
    public async Task Index_NoMaintenanceMasterDataPermission_ReturnsForbid()
    {
        // QAManager طبق Seed موجود هیچ Grant ای روی ماژول "MaintenanceMasterData" ندارد
        // (بر خلاف Viewer که یک ردیف View فقط‌خواندنی روی همین ماژول دارد).
        var (ctx, perms, user) = await CreateInfra(nameof(Index_NoMaintenanceMasterDataPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = new EquipmentClassificationController(new FakeClassificationService(), user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_RoleWithMaintenanceMasterDataView_ReturnsView()
    {
        // MaintenanceEngineer طبق Seed موجود روی "MaintenanceMasterData" دسترسی View دارد
        var (ctx, perms, user) = await CreateInfra(nameof(Index_RoleWithMaintenanceMasterDataView_ReturnsView), role: SystemRoles.MaintenanceEngineer);
        var controller = new EquipmentClassificationController(new FakeClassificationService(), user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_SystemAdmin_ReturnsView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_SystemAdmin_ReturnsView), role: SystemRoles.SystemAdmin);
        var controller = new EquipmentClassificationController(new FakeClassificationService(), user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }
}
