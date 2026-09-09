using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
/// طبق «PEMS Phase 5 — Final Reliability Intelligence» بند ۴۶: ReliabilityController پیش از
/// این فاز هیچ بررسی مجوزی نداشت (خلأ امنیتی واقعی، طبق اصل «Hidden Navigation != Security»
/// همین پروژه که از همون تسک اول این جلسه تثبیت شد) - این تست‌ها اثبات می‌کنند گیت جدید واقعاً
/// کار می‌کند.
/// </summary>
public class ReliabilityAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ReliabilityAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>(), UserName = "tester" };
        return (ctx, permissionService, currentUser);
    }

    private static ReliabilityController CreateController(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms) =>
        new ReliabilityController(new ReliabilityService(ctx), new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx)),
            new ReliabilityDataQualityService(ctx), user, perms);

    [Fact]
    public async Task Index_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Index(null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Index(null, null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task DataQuality_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(DataQuality_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.DataQuality();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DataQuality_MaintenanceEngineer_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(DataQuality_MaintenanceEngineer_CanView), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.DataQuality();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ClassDetail_UnknownClass_ReturnsNotFound_ForAuthorizedUser()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ClassDetail_UnknownClass_ReturnsNotFound_ForAuthorizedUser), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ClassDetail(99999);

        Assert.IsType<NotFoundResult>(result);
    }
}
