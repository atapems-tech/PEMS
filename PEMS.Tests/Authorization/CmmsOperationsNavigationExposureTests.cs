using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS CMMS Sidebar Refactoring»: CmmsOperationsController.Backlog از قبل کاملاً مجوزدار بود
/// (همون چک "WorkOrder"/"View" که Dashboard دارد) اما هیچ ورودی سایدباری نداشت - دقیقاً هم‌الگو با
/// ShiftHandoverNavigationExposureTests از فاز 2C-1. این تست‌ها ثابت می‌کنند لینک تازه‌افزوده‌شده
/// (زیر گروه «Maintenance Planning») هرگز کاربری را به یک صفحه‌ی Forbid هدایت نمی‌کند، و این‌که
/// افزودن لینک هیچ مرز امنیتی تازه‌ای باز نکرد.
/// </summary>
public class CmmsOperationsNavigationExposureTests
{
    private class SpyCmmsOperationsService : ICmmsOperationsService
    {
        public Task<CmmsOperationsDashboardDto> GetDashboardAsync() => Task.FromResult(new CmmsOperationsDashboardDto());

        public Task<PagedResult<MaintenanceBacklogItemDto>> GetBacklogAsync(
            string? status = null, string? priority = null, int? equipmentId = null,
            bool overdueOnly = false, int page = 1, int? pageSize = null) =>
            Task.FromResult(new PagedResult<MaintenanceBacklogItemDto> { Items = new List<MaintenanceBacklogItemDto>(), Page = page, PageSize = pageSize ?? 25, TotalCount = 0 });
    }

    private static async Task<CmmsOperationsController> CreateController(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"CmmsOpsNav_{dbName}");
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

        return new CmmsOperationsController(new SpyCmmsOperationsService(), currentUser, permissionService).WithTempData();
    }

    // هر نقشی که بخش CMMS Integration سایدبار را می‌بیند (SystemAdmin, EngineeringManager,
    // EngineeringSupervisor, MaintenanceEngineer طبق MenuAccess.CmmsIntegration بدون تغییر در این
    // فاز) باید واقعاً بتواند لینک تازه‌افزوده‌شده‌ی Backlog را باز کند.
    [Theory]
    [InlineData(SystemRoles.EngineeringManager)]
    [InlineData(SystemRoles.EngineeringSupervisor)]
    [InlineData(SystemRoles.MaintenanceEngineer)]
    public async Task Backlog_EveryRoleThatSeesTheCmmsSidebarSection_CanActuallyOpenTheLink(string role)
    {
        var controller = await CreateController(nameof(Backlog_EveryRoleThatSeesTheCmmsSidebarSection_CanActuallyOpenTheLink) + role, role);

        var result = await controller.Backlog(null, null, null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Backlog_NoRole_ReturnsForbid()
    {
        var controller = await CreateController(nameof(Backlog_NoRole_ReturnsForbid), role: null);

        var result = await controller.Backlog(null, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    // مجوز صفحه مستقل از نمایش/عدم‌نمایش لینک است - افزودن لینک، مرز امنیتی تازه‌ای باز نکرد.
    [Fact]
    public async Task Backlog_RoleWithNoWorkOrderPermission_ReturnsForbid_EvenThoughItIsNowLinked()
    {
        var controller = await CreateController(nameof(Backlog_RoleWithNoWorkOrderPermission_ReturnsForbid_EvenThoughItIsNowLinked), SystemRoles.ContractManager);

        var result = await controller.Backlog(null, null, null);

        Assert.IsType<ForbidResult>(result);
    }
}
