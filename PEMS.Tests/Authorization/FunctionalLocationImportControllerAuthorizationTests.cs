using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «Functional Location Master Data Import» بند ۱۵: همان الگوی EquipmentImportController -
/// Upload/Review با Create/View؛ ToggleRow، Commit (Approve) و Discard (Delete) عملیات تعریف‌محور هستند (SCRUM-698).
/// </summary>
public class FunctionalLocationImportControllerAuthorizationTests
{
    private static async Task<(FunctionalLocationImportController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"FLImportAuth_{dbName}");
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
        var importService = new FunctionalLocationImportService(ctx);

        var controller = new FunctionalLocationImportController(importService, ctx, currentUser, permissionService).WithTempData();
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

    // SCRUM-698: commit, discard and the row toggle are operations; the gate is the operation permission the host checks.
    private static string? PermissionOf(string method)
        => ((PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IFunctionalLocationImportOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission;

    private static async Task<bool> MayAsync(string dbName, string? role, string method)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"FLImportOps_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();
        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        await permissionService.SeedDefaultsAsync();
        var parts = PermissionOf(method)!.Split('.');
        return await permissionService.HasPermissionAsync(role != null ? new List<string> { role } : new List<string>(), parts[0], parts[1]);
    }

    [Fact]
    public async Task Commit_RoleWithoutApprove_IsNotAllowedByTheOperationPermission()
        => Assert.False(await MayAsync(nameof(Commit_RoleWithoutApprove_IsNotAllowedByTheOperationPermission), SystemRoles.QAManager, "CommitAsync"));

    [Fact]
    public async Task Commit_MaintenanceEngineer_HasApprove_AndMissingBatchStillThrows()
    {
        Assert.True(await MayAsync(nameof(Commit_MaintenanceEngineer_HasApprove_AndMissingBatchStillThrows), SystemRoles.MaintenanceEngineer, "CommitAsync"));
        var ops = new PEMS.Web.Services.FunctionalLocationImportOperations(new FunctionalLocationImportService(PemsDbContextFactory.CreateInMemory("FLImportOps_commit")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.CommitAsync(99999));
    }

    [Fact]
    public async Task Discard_ViewerRole_IsNotAllowedByTheOperationPermission()
        => Assert.False(await MayAsync(nameof(Discard_ViewerRole_IsNotAllowedByTheOperationPermission), SystemRoles.Viewer, "DiscardAsync"));

    [Fact]
    public async Task Discard_EngineeringManager_IsAllowed_AndMissingBatchIsSilent()
    {
        Assert.True(await MayAsync(nameof(Discard_EngineeringManager_IsAllowed_AndMissingBatchIsSilent), SystemRoles.EngineeringManager, "DiscardAsync"));
        var ops = new PEMS.Web.Services.FunctionalLocationImportOperations(new FunctionalLocationImportService(PemsDbContextFactory.CreateInMemory("FLImportOps_discard")));
        Assert.True((await ops.DiscardAsync(99999)).Success);
    }

    [Fact]
    public async Task ToggleRow_ViewerRole_IsNotAllowedByTheOperationPermission()
        => Assert.False(await MayAsync(nameof(ToggleRow_ViewerRole_IsNotAllowedByTheOperationPermission), SystemRoles.Viewer, "ToggleRowAsync"));

    [Fact]
    public async Task Review_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Review_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Review(1);

        Assert.IsType<ForbidResult>(result);
    }
}
