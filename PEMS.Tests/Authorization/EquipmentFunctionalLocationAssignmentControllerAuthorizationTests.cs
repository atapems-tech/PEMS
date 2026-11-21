using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS CMMS — Equipment Functional Location Assignment &amp; Import» بند «Authorization»:
/// همون الگوی FunctionalLocationImportControllerAuthorizationTests - Upload/Review/ToggleRow/
/// ReplaceExisting با Create/Approve مناسب، Commit (پرریسک‌ترین) با Approve، Discard با Delete.
/// </summary>
public class EquipmentFunctionalLocationAssignmentControllerAuthorizationTests
{
    private static async Task<(EquipmentFunctionalLocationAssignmentController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"EqFlAssignAuth_{dbName}");
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
        var assignmentService = new EquipmentFunctionalLocationAssignmentService(ctx);

        var controller = new EquipmentFunctionalLocationAssignmentController(assignmentService, ctx, currentUser, permissionService).WithTempData();
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

    // SCRUM-694: toggle, replace-existing, commit and discard are operations; the action host refuses a caller without the operation permission, else it calls the operation.
    private static async Task<bool> MayAsync(string dbName, string? role, string method)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"EqFlAssignOps_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();
        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        await permissionService.SeedDefaultsAsync();
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());
        var permission = ((PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IEquipmentFunctionalLocationAssignmentOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!.Split('.');
        return await permissionService.HasPermissionAsync(role != null ? new List<string> { role } : new List<string>(), permission[0], permission[1]);
    }

    private static PEMS.Web.Services.EquipmentFunctionalLocationAssignmentOperations Ops(string dbName)
        => new(new EquipmentFunctionalLocationAssignmentService(PemsDbContextFactory.CreateInMemory($"EqFlAssignOpsRun_{dbName}")));

    [Fact]
    public async Task ToggleRow_ViewerRole_IsNotAllowedByTheOperationPermission()
        => Assert.False(await MayAsync(nameof(ToggleRow_ViewerRole_IsNotAllowedByTheOperationPermission), SystemRoles.Viewer, "ToggleRowAsync"));

    [Fact]
    public async Task ReplaceExisting_ViewerRole_IsNotAllowed_AndNeedsApprove()
    {
        // existing assignment protection: replacing an existing assignment needs Approve, not just Create
        Assert.False(await MayAsync(nameof(ReplaceExisting_ViewerRole_IsNotAllowed_AndNeedsApprove), SystemRoles.Viewer, "ReplaceExistingAsync"));
    }

    [Fact]
    public async Task ReplaceExisting_MaintenanceEngineer_HasApprove_AndMissingRowIsSilent()
    {
        Assert.True(await MayAsync(nameof(ReplaceExisting_MaintenanceEngineer_HasApprove_AndMissingRowIsSilent), SystemRoles.MaintenanceEngineer, "ReplaceExistingAsync"));
        Assert.True((await Ops("replace").ReplaceExistingAsync(99999, true)).Success);
    }

    [Fact]
    public async Task Commit_QAManagerWithoutModulePermission_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Commit_QAManagerWithoutModulePermission_IsNotAllowed), SystemRoles.QAManager, "CommitAsync"));

    [Fact]
    public async Task Commit_MaintenanceEngineer_HasApprove_AndMissingBatchStillThrows()
    {
        Assert.True(await MayAsync(nameof(Commit_MaintenanceEngineer_HasApprove_AndMissingBatchStillThrows), SystemRoles.MaintenanceEngineer, "CommitAsync"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops("commit").CommitAsync(99999));
    }

    [Fact]
    public async Task Discard_ViewerRole_IsNotAllowedByTheOperationPermission()
        => Assert.False(await MayAsync(nameof(Discard_ViewerRole_IsNotAllowedByTheOperationPermission), SystemRoles.Viewer, "DiscardAsync"));

    [Fact]
    public async Task Discard_EngineeringManager_IsAllowed_AndMissingBatchIsSilent()
    {
        Assert.True(await MayAsync(nameof(Discard_EngineeringManager_IsAllowed_AndMissingBatchIsSilent), SystemRoles.EngineeringManager, "DiscardAsync"));
        Assert.True((await Ops("discard").DiscardAsync(99999)).Success);
    }
}
