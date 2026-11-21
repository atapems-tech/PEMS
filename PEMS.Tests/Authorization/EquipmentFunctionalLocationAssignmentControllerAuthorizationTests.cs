using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS CMMS — Equipment Functional Location Assignment &amp; Import»: همان الگوی FunctionalLocationImportControllerAuthorizationTests -
/// خواندن/Review با View، Upload/ToggleRow با Create، ReplaceExisting و Commit (پرریسک‌ترین) با Approve، Discard با Delete.
/// SCRUM-694: کنترلر حذف شد؛ عملیات تعریف‌محور هستند و گیت، مجوز عملیات است (action host پیش از صدا زدن عملیات آن را می‌سنجد).
/// </summary>
public class EquipmentFunctionalLocationAssignmentControllerAuthorizationTests
{
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
        var permission = ((PEMS.Application.Common.PemsOperationAttribute)typeof(IEquipmentFunctionalLocationAssignmentOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!.Split('.');
        return await permissionService.HasPermissionAsync(role != null ? new List<string> { role } : new List<string>(), permission[0], permission[1]);
    }

    private static PEMS.Web.Services.EquipmentFunctionalLocationAssignmentOperations Ops(string dbName)
        => new(new EquipmentFunctionalLocationAssignmentService(PemsDbContextFactory.CreateInMemory($"EqFlAssignOpsRun_{dbName}")));

    [Fact]
    public async Task Batches_NoPermission_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Batches_NoPermission_IsNotAllowed), null, "BatchesAsync"));

    [Fact]
    public async Task Batches_ViewerRole_CanView_AndNoBatchesYet()
    {
        Assert.True(await MayAsync(nameof(Batches_ViewerRole_CanView_AndNoBatchesYet), SystemRoles.Viewer, "BatchesAsync"));
        Assert.Empty(await Ops("batches").BatchesAsync());
    }

    [Fact]
    public async Task Upload_ViewerRole_IsNotAllowed_AndAnEmptyFileIsRefused()
    {
        Assert.False(await MayAsync(nameof(Upload_ViewerRole_IsNotAllowed_AndAnEmptyFileIsRefused), SystemRoles.Viewer, "UploadAsync"));
        Assert.False((await Ops("upload").UploadAsync(null, [], "x.xlsx")).Success);
    }

    [Fact]
    public async Task Rows_NoPermission_IsNotAllowed_AndAMissingBatchHasNoRows()
    {
        Assert.False(await MayAsync(nameof(Rows_NoPermission_IsNotAllowed_AndAMissingBatchHasNoRows), null, "RowsAsync"));
        Assert.Empty(await Ops("rows").RowsAsync(99999));
    }

    [Fact]
    public async Task ToggleRow_ViewerRole_IsNotAllowedByTheOperationPermission()
        => Assert.False(await MayAsync(nameof(ToggleRow_ViewerRole_IsNotAllowedByTheOperationPermission), SystemRoles.Viewer, "ToggleRowAsync"));

    [Fact]
    public async Task ReplaceExisting_ViewerRole_IsNotAllowed_AndNeedsApprove()
        => Assert.False(await MayAsync(nameof(ReplaceExisting_ViewerRole_IsNotAllowed_AndNeedsApprove), SystemRoles.Viewer, "ReplaceExistingAsync"));

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
