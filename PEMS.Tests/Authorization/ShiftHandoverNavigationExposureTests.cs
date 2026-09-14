using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 2C-1 — Navigation Integrity»: `ShiftHandoverController` قبلاً هیچ ورودی
/// سایدباری نداشت اما از قبل کاملاً مجوزدار بود (`RoleModuleActionService`، ماژول
/// "ShiftHandover" - `PEMS.Persistence/Servises/RoleModuleActionService.cs`). این تست‌ها
/// ثابت می‌کنند لینک تازه‌افزوده‌شده در `_SidebarMenu.cshtml` (گیت‌شده با همان
/// `MenuAccess.CmmsIntegration` سطح‌بخش که بدون تغییر باقی مانده) هرگز کاربری را به یک صفحه‌ی
/// Forbid هدایت نمی‌کند، و این‌که خودِ کنترلر همچنان مستقل از نمایش/عدم‌نمایش لینک، مجوز واقعی
/// را اعمال می‌کند - یعنی افزودن این لینک هرگز نباید به‌عنوان یک مرز امنیتی جدید تلقی شود.
/// </summary>
public class ShiftHandoverNavigationExposureTests
{
    private class SpyShiftHandoverService : IShiftHandoverService
    {
        public Task<List<PEMS.Application.DTOs.ShiftHandoverListDto>> GetAllAsync(string? status) =>
            Task.FromResult(new List<PEMS.Application.DTOs.ShiftHandoverListDto>());

        public Task<PEMS.Application.DTOs.ShiftHandoverDetailDto?> GetDetailsAsync(int id) =>
            Task.FromResult<PEMS.Application.DTOs.ShiftHandoverDetailDto?>(null);

        public Task<(bool Success, string? Error, int? Id)> CreateAsync(
            int outgoingEmployeeId, int incomingEmployeeId, string? shiftLabel, DateTime handoverDate, string? notes) =>
            throw new NotImplementedException();

        public Task<(bool Success, string? Error)> PrepareAsync(int id, int? actorEmployeeId) => throw new NotImplementedException();
        public Task<(bool Success, string? Error)> AcknowledgeAsync(int id, int? actorEmployeeId) => throw new NotImplementedException();
        public Task<(bool Success, string? Error)> CompleteAsync(int id, int? actorEmployeeId) => throw new NotImplementedException();
    }

    private static async Task<ShiftHandoverController> CreateController(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ShiftHandoverNav_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var roleModuleActionService = new RoleModuleActionService(ctx, accessControl);
        await roleModuleActionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };

        return new ShiftHandoverController(new SpyShiftHandoverService(), roleModuleActionService, ctx, currentUser)
            .WithTempData();
    }

    // ==============================================================================
    // هر نقشی که بخش CMMS Integration سایدبار (و بنابراین لینک تازه‌افزوده‌شده) را می‌بیند،
    // باید واقعاً بتواند Index را باز کند - طبق MenuAccess.CmmsIntegration (بدون تغییر در این
    // فاز): SystemAdmin, EngineeringManager, EngineeringSupervisor, MaintenanceEngineer.
    // ==============================================================================

    [Theory]
    [InlineData(SystemRoles.EngineeringManager)]
    [InlineData(SystemRoles.EngineeringSupervisor)]
    [InlineData(SystemRoles.MaintenanceEngineer)]
    public async Task Index_EveryRoleThatSeesTheCmmsSidebarSection_CanActuallyOpenTheLink(string role)
    {
        var controller = await CreateController(nameof(Index_EveryRoleThatSeesTheCmmsSidebarSection_CanActuallyOpenTheLink) + role, role);

        var result = await controller.Index(null);

        Assert.IsType<ViewResult>(result);
    }

    // ==============================================================================
    // برعکس، یک نقش که اصلاً بخش CMMS Integration را نمی‌بیند (طبق MenuAccess.CmmsIntegration)
    // و مجوز ShiftHandover هم برایش Seed نشده (مثل ContractManager)، باید همچنان با Forbid روبه‌رو
    // شود - یعنی مجوز واقعی مستقل از نمایش/عدم‌نمایش لینک است، نه این‌که لینک خودش یک دروازه‌ی
    // امنیتی باشد.
    // ==============================================================================

    [Fact]
    public async Task Index_RoleWithNoSeededShiftHandoverPermission_ReturnsForbid()
    {
        var controller = await CreateController(nameof(Index_RoleWithNoSeededShiftHandoverPermission_ReturnsForbid), SystemRoles.ContractManager);

        var result = await controller.Index(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_NoRole_ReturnsForbid()
    {
        var controller = await CreateController(nameof(Index_NoRole_ReturnsForbid), role: null);

        var result = await controller.Index(null);

        Assert.IsType<ForbidResult>(result);
    }

    // ==============================================================================
    // Viewer/QAManager طبق Seed واقعی فقط "View" دارند - یعنی می‌توانند صفحه را باز کنند حتی
    // اگر MenuAccess.CmmsIntegration (که تغییر نکرد) لینک را برایشان نشان ندهد. این رفتار
    // *ناهماهنگی محافظه‌کارانه* است (کم‌نمایشی، نه بیش‌دسترسی) - دقیقاً همان الگوی موجود در کل
    // این فایل سایدبار برای سایر ماژول‌ها، نه یک مشکل تازه‌ی این فاز.
    // ==============================================================================

    [Theory]
    [InlineData(SystemRoles.Viewer)]
    [InlineData(SystemRoles.QAManager)]
    public async Task Index_RoleWithViewOnlyPermission_CanOpenPage_EvenThoughSidebarLinkIsHiddenForThem(string role)
    {
        var controller = await CreateController(nameof(Index_RoleWithViewOnlyPermission_CanOpenPage_EvenThoughSidebarLinkIsHiddenForThem) + role, role);

        var result = await controller.Index(null);

        Assert.IsType<ViewResult>(result);
    }

    [Theory]
    [InlineData(SystemRoles.Viewer)]
    [InlineData(SystemRoles.QAManager)]
    public async Task Create_RoleWithViewOnlyPermission_ReturnsForbid_NoCreateGranted(string role)
    {
        var controller = await CreateController(nameof(Create_RoleWithViewOnlyPermission_ReturnsForbid_NoCreateGranted) + role, role);

        var result = await controller.Create();

        Assert.IsType<ForbidResult>(result);
    }
}
