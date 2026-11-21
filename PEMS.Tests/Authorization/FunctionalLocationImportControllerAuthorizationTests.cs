using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «Functional Location Master Data Import» بند ۱۵: همان الگوی EquipmentImportController - خواندن/Review با View،
/// Upload/ToggleRow با Create، Commit (پرریسک‌ترین) با Approve، Discard با Delete. SCRUM-701: کنترلر حذف شد؛ همه عملیات
/// تعریف‌محور هستند و گیت، مجوز عملیات است (action host پیش از صدا زدن عملیات آن را می‌سنجد).
/// </summary>
public class FunctionalLocationImportControllerAuthorizationTests
{
    private static string PermissionOf(string method)
        => ((PEMS.Application.Common.PemsOperationAttribute)typeof(IFunctionalLocationImportOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!;

    private static async Task<bool> MayAsync(string dbName, string? role, string method)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"FLImportOps_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();
        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        await permissionService.SeedDefaultsAsync();
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());
        var parts = PermissionOf(method).Split('.');
        return await permissionService.HasPermissionAsync(role != null ? new List<string> { role } : new List<string>(), parts[0], parts[1]);
    }

    private static PEMS.Web.Services.FunctionalLocationImportOperations Ops(string dbName)
        => new(new FunctionalLocationImportService(PemsDbContextFactory.CreateInMemory($"FLImportOpsRun_{dbName}")));

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
        var result = await Ops("upload").UploadAsync(null, [], "x.xlsx");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Rows_NoPermission_IsNotAllowed_AndAMissingBatchHasNoRows()
    {
        Assert.False(await MayAsync(nameof(Rows_NoPermission_IsNotAllowed_AndAMissingBatchHasNoRows), null, "RowsAsync"));
        Assert.Empty(await Ops("rows").RowsAsync(99999));
    }

    [Fact]
    public async Task Commit_QAManagerWithoutApprove_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Commit_QAManagerWithoutApprove_IsNotAllowed), SystemRoles.QAManager, "CommitAsync"));

    [Fact]
    public async Task Commit_MaintenanceEngineer_HasApprove_AndMissingBatchStillThrows()
    {
        Assert.True(await MayAsync(nameof(Commit_MaintenanceEngineer_HasApprove_AndMissingBatchStillThrows), SystemRoles.MaintenanceEngineer, "CommitAsync"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops("commit").CommitAsync(99999));
    }

    [Fact]
    public async Task Discard_ViewerRole_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Discard_ViewerRole_IsNotAllowed), SystemRoles.Viewer, "DiscardAsync"));

    [Fact]
    public async Task Discard_EngineeringManager_IsAllowed_AndMissingBatchIsSilent()
    {
        Assert.True(await MayAsync(nameof(Discard_EngineeringManager_IsAllowed_AndMissingBatchIsSilent), SystemRoles.EngineeringManager, "DiscardAsync"));
        Assert.True((await Ops("discard").DiscardAsync(99999)).Success);
    }

    [Fact]
    public async Task ToggleRow_ViewerRole_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(ToggleRow_ViewerRole_IsNotAllowed), SystemRoles.Viewer, "ToggleRowAsync"));
}
