using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 2B»: EquipmentController پیش از این فاز هیچ بررسی مجوزی نداشت. داده‌ی
/// RolePermissions برای ماژول "Equipment" از قبل در PermissionService.SeedDefaultsAsync وجود
/// داشت (فقط MaintenanceEngineer: Create/Edit) ولی هرگز به کنترلر وصل نشده بود. این تست‌ها
/// ثابت می‌کنند گیت جدید (Create + AssignFoundation) کار می‌کند - و طبق تصمیم صریح کاربر،
/// Index/Details دست‌نخورده و برای همه‌ی نقش‌ها در دسترس می‌مانند.
/// </summary>
public class EquipmentControllerAuthorizationTests
{
    private class SpyEquipmentService : IEquipmentService
    {
        public bool CreateCalled;
        public bool AssignFoundationCalled;

        public Task<List<EquipmentDto>> GetAllAsync(
            int? projectId = null, int? disciplineId = null, string? equipmentType = null,
            string? status = null, string? q = null) => Task.FromResult(new List<EquipmentDto>());

        public Task<PagedResult<EquipmentDto>> GetPagedAsync(
            int page = 1, int pageSize = 50, int? projectId = null, int? disciplineId = null,
            string? equipmentType = null, string? status = null, string? q = null) =>
            Task.FromResult(new PagedResult<EquipmentDto> { Page = page, PageSize = pageSize });

        public Task<EquipmentDashboardDto> GetDashboardAsync(int? projectId = null) => Task.FromResult(new EquipmentDashboardDto());

        public Task<int> CreateAsync(Equipment equipment)
        {
            CreateCalled = true;
            return Task.FromResult(1);
        }

        public Task<EquipmentPassportDto?> GetPassportAsync(int equipmentId) => throw new NotImplementedException();

        public Task<(bool Success, string? Error)> AssignFoundationAsync(
            int equipmentId, int? functionalLocationId, int? parentEquipmentId, DateTime? installationDate, string? currentUserName)
        {
            AssignFoundationCalled = true;
            return Task.FromResult<(bool, string?)>((true, null));
        }
    }






    // SCRUM-692: the create is the entity action over Equipment with the permission Equipment.Create; the reads are operations without a module permission (signed-in users, as before).
    private static async Task<bool> MayAsync(string dbName, string? role, string module, string action)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"EquipmentAuth_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();
        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        await permissionService.SeedDefaultsAsync();
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());
        return await permissionService.HasPermissionAsync(role != null ? new List<string> { role } : new List<string>(), module, action);
    }

    [Fact]
    public async Task Create_ViewerRole_IsNotAllowed()
        => Assert.False(await MayAsync(nameof(Create_ViewerRole_IsNotAllowed), SystemRoles.Viewer, "Equipment", "Create"));

    [Fact]
    public async Task Create_MaintenanceEngineer_IsAllowed()
        => Assert.True(await MayAsync(nameof(Create_MaintenanceEngineer_IsAllowed), SystemRoles.MaintenanceEngineer, "Equipment", "Create"));

    [Fact]
    public async Task List_AndPassport_ReadThroughTheService_WithoutAPermissionGate()
    {
        var ctx = PemsDbContextFactory.CreateInMemory("EquipmentAuth_reads");
        var ops = new PEMS.Web.Services.EquipmentPagesOperations(ctx, new SpyEquipmentService(), null!, null!, null!);
        Assert.Empty(await ops.ListAsync());
        Assert.Empty(await ops.ProjectOptionsAsync());
    }

    // AssignFoundation is the operation Equipment.AssignFoundation (permission Equipment.Edit, SCRUM-692): the operation host refuses a role without it; the operation calls the service.
    [Fact]
    public async Task AssignFoundationOperation_CallsTheService()
    {
        var spy = new SpyEquipmentService();
        var ops = new PEMS.Web.Services.ReliabilityRegisterOperations(null!, spy, null!, new FakeCurrentUserService { UserName = "tester" });

        var result = await ops.AssignFoundationAsync(1);

        Assert.True(result.Success);
        Assert.True(spy.AssignFoundationCalled);
    }
}
