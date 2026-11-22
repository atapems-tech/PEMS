using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۲۸: هیچ ماژول مجوز جدیدی ساخته نشد - از همان "MaintenanceMasterData" موجود
/// استفاده می‌شود (View برای Dashboard/Queue/Coverage، Edit برای Accept/Reject). SCRUM-691: کنترلر حذف شد؛ خواندن‌ها عملیات تعریف‌محور هستند
/// (IRegisterPagesOperations) و گیت، مجوز عملیات است؛ action host پیش از صدا زدن عملیات آن را می‌سنجد.
/// </summary>
public class EquipmentClassificationAuthorizationTests
{
    private static string PermissionOf(string method)
        => ((PEMS.Application.Common.PemsOperationAttribute)typeof(IRegisterPagesOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!;

    private static async Task<bool> MayAsync(string dbName, string? role, string method)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"EquipClassAuth_{dbName}");
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

    private class FakeClassificationService : IEquipmentClassificationService
    {
        public Task<ClassificationDashboardDto> GetDashboardAsync() => Task.FromResult(new ClassificationDashboardDto { TotalActiveEquipment = 3, ClassifiedCount = 2, UnclassifiedCount = 1,
            ByConfidence = new() { new CountByKeyDto { Key = "High", Count = 2 } } });
        public Task<ReviewQueuePageDto> GetReviewQueueAsync(int page, int pageSize, string statusFilter = "PENDING") => Task.FromResult(new ReviewQueuePageDto());
        public Task<ReviewQueueItemDto?> GetPendingSuggestionForEquipmentAsync(int equipmentId) => Task.FromResult<ReviewQueueItemDto?>(null);
        public Task<ClassificationExplanationDto?> ExplainClassificationAsync(int equipmentId) => Task.FromResult<ClassificationExplanationDto?>(null);
        public Task<BackfillResultDto> BackfillAsync() => Task.FromResult(new BackfillResultDto());
        public Task<bool> AcceptSuggestionAsync(int suggestionId, string reviewedBy, string? comment) => Task.FromResult(true);
        public Task<bool> RejectSuggestionAsync(int suggestionId, string reviewedBy, string? comment) => Task.FromResult(true);
    }

    private class FakeCoverageService : IEquipmentClassCoverageService
    {
        public Task<MaintenanceIntelligenceCoverageSummaryDto> GetCoverageMatrixAsync() => Task.FromResult(new MaintenanceIntelligenceCoverageSummaryDto());
    }

    private static PEMS.Web.Services.RegisterPagesOperations Ops(string dbName)
        => new(PemsDbContextFactory.CreateInMemory($"EquipClassOps_{dbName}"), new FakeClassificationService(), new FakeCoverageService());

    [Fact]
    public async Task Summary_NoMaintenanceMasterDataPermission_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Summary_NoMaintenanceMasterDataPermission_IsNotAllowed), SystemRoles.QAManager, "ClassificationSummaryAsync"));

    [Fact]
    public async Task Summary_RoleWithMaintenanceMasterDataView_IsAllowed_AndTheCountsAreReturned()
    {
        Assert.True(await MayAsync(nameof(Summary_RoleWithMaintenanceMasterDataView_IsAllowed_AndTheCountsAreReturned), SystemRoles.Viewer, "ClassificationSummaryAsync"));
        var row = Assert.Single(await Ops("summary").ClassificationSummaryAsync());
        Assert.Equal(3, row.TotalActiveEquipment);
    }

    [Fact]
    public async Task Summary_SystemAdmin_IsAllowed()
        => Assert.True(await MayAsync(nameof(Summary_SystemAdmin_IsAllowed), SystemRoles.SystemAdmin, "ClassificationSummaryAsync"));

    [Fact]
    public async Task Breakdown_ListsEveryGroupOfTheDashboard()
    {
        var rows = await Ops("breakdown").ClassificationBreakdownAsync();
        Assert.Contains(rows, r => r.Group == "By confidence" && r.Key == "High" && r.Count == 2);
    }

    [Fact]
    public async Task ReviewQueue_NoPermission_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(ReviewQueue_NoPermission_IsNotAllowed), null, "ReviewQueueAsync"));

    [Fact]
    public async Task ReviewQueue_ViewerRole_CanSeeTheQueue_ButAcceptRequiresEdit()
    {
        // Viewer فقط View دارد نه Edit - باید صف را ببیند ولی نتواند Accept کند (Accept/Reject عملیات EquipmentClassification با MaintenanceMasterData.Edit هستند)
        Assert.True(await MayAsync(nameof(ReviewQueue_ViewerRole_CanSeeTheQueue_ButAcceptRequiresEdit), SystemRoles.Viewer, "ReviewQueueAsync"));
        Assert.Empty(await Ops("queue").ReviewQueueAsync());
    }

    [Fact]
    public async Task Coverage_NoPermission_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Coverage_NoPermission_IsNotAllowed), SystemRoles.QAManager, "CoverageSummaryAsync"));

    [Fact]
    public async Task Coverage_RoleWithView_IsAllowed()
    {
        Assert.True(await MayAsync(nameof(Coverage_RoleWithView_IsAllowed), SystemRoles.Viewer, "CoverageSummaryAsync"));
        Assert.Single(await Ops("coverage").CoverageSummaryAsync());
    }
}
