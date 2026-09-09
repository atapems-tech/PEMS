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
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۲۸: این Controller هیچ ماژول
/// مجوز جدیدی نساخته - از همان "MaintenanceMasterData" موجود استفاده می‌کند (View برای
/// Dashboard/Queue، Edit برای Accept/Reject). این تست‌ها اثبات می‌کنند گیت واقعاً روی همون مجوز
/// است و کاربر فقط‌خواندنی نمی‌تواند طبقه‌بندی معتبر را تغییر دهد.
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

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>(), UserName = "tester" };
        return (ctx, permissionService, currentUser);
    }

    private class FakeClassificationService : IEquipmentClassificationService
    {
        public Task<ClassificationDashboardDto> GetDashboardAsync() => Task.FromResult(new ClassificationDashboardDto());
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

    private static EquipmentClassificationController CreateController(IEquipmentClassificationService classificationService,
        ICurrentUserService user, IPermissionService perms) =>
        new EquipmentClassificationController(classificationService, new FakeCoverageService(), user, perms).WithTempData();

    [Fact]
    public async Task Index_NoMaintenanceMasterDataPermission_ReturnsForbid()
    {
        // QAManager طبق Seed موجود هیچ Grant ای روی ماژول "MaintenanceMasterData" ندارد
        // (بر خلاف Viewer که یک ردیف View فقط‌خواندنی روی همین ماژول دارد).
        var (ctx, perms, user) = await CreateInfra(nameof(Index_NoMaintenanceMasterDataPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_RoleWithMaintenanceMasterDataView_ReturnsView()
    {
        // MaintenanceEngineer طبق Seed موجود روی "MaintenanceMasterData" دسترسی View دارد
        var (ctx, perms, user) = await CreateInfra(nameof(Index_RoleWithMaintenanceMasterDataView_ReturnsView), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_SystemAdmin_ReturnsView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_SystemAdmin_ReturnsView), role: SystemRoles.SystemAdmin);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ReviewQueue_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewQueue_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.ReviewQueue();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ReviewQueue_ViewerRole_CanSeeQueue_ButAcceptRequiresEdit()
    {
        // Viewer فقط View دارد نه Edit - باید صف را ببیند ولی نتواند Accept کند
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewQueue_ViewerRole_CanSeeQueue_ButAcceptRequiresEdit), role: SystemRoles.Viewer);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var viewResult = await controller.ReviewQueue();
        Assert.IsType<ViewResult>(viewResult);

        var acceptResult = await controller.Accept(1, null);
        Assert.IsType<ForbidResult>(acceptResult);
    }

    [Fact]
    public async Task Accept_RoleWithEditPermission_Succeeds()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Accept_RoleWithEditPermission_Succeeds), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Accept(1, null);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task Reject_NoEditPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Reject_NoEditPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Reject(1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Coverage_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Coverage_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Coverage();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Coverage_RoleWithView_ReturnsView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Coverage_RoleWithView_ReturnsView), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(new FakeClassificationService(), user, perms);

        var result = await controller.Coverage();

        Assert.IsType<ViewResult>(result);
    }
}
