using PEMS.Web.Areas.Warehouse.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: TeamController/
/// CrewController/DisciplineController/InventoryMasterDataController/MaintenanceMasterDataController
/// هیچ‌کدام بررسی مجوزی نداشتند - CreateTeam/AddMember/RemoveMember/MoveMember/AddTask/
/// SetTaskStatus/Create*/Toggle*Active برای هر کاربر احرازهویت‌شده‌ای باز بودند. همون الگوی
/// ImportPipelineAuthorizationTests.cs/AIPipelineAuthorizationTests.cs.
/// </summary>
public class OrganizationMasterDataAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"OrgMasterDataAuth_{dbName}");
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
        return (ctx, permissionService, currentUser);
    }

    private class FakeTeamService : ITeamService
    {
        public bool AddOrReactivateCalled;
        public bool RemoveCalled;
        public bool MoveCalled;
        public Task AddOrReactivateMemberAsync(int teamId, int employeeId, int roleId) { AddOrReactivateCalled = true; return Task.CompletedTask; }
        public Task RemoveMemberAsync(int teamMemberId) { RemoveCalled = true; return Task.CompletedTask; }
        public Task MoveMemberAsync(int employeeId, int fromTeamId, int toTeamId, int roleId) { MoveCalled = true; return Task.CompletedTask; }
    }

    private class FakeCrewService : ICrewService
    {
        public bool CreateCalled;
        public bool AddOrReactivateCalled;
        public bool RemoveCalled;
        public Task<(bool Success, string? Error, int? CrewId)> CreateAsync(string code, string name, int? workOrderId)
        { CreateCalled = true; return Task.FromResult<(bool, string?, int?)>((true, null, 1)); }
        public Task AddOrReactivateMemberAsync(int crewId, int employeeId, int roleId) { AddOrReactivateCalled = true; return Task.CompletedTask; }
        public Task RemoveMemberAsync(int crewMemberId) { RemoveCalled = true; return Task.CompletedTask; }
    }

    private class FakeDisciplineWorkspaceService : IDisciplineWorkspaceService
    {
        public bool AddTaskCalled;
        public bool SetTaskStatusCalled;
        public Task<List<DisciplineSummaryDto>> GetSummaryListAsync() => Task.FromResult(new List<DisciplineSummaryDto>());
        public Task<DisciplineWorkspaceDto?> GetWorkspaceAsync(int disciplineId, int? projectId = null) => Task.FromResult<DisciplineWorkspaceDto?>(null);
        public Task<int> AddTaskAsync(int disciplineId, int? projectId, string title, string? description, string type, int? responsibleEmployeeId, DateTime? dueDate)
        { AddTaskCalled = true; return Task.FromResult(1); }
        public Task SetTaskStatusAsync(int taskId, string status) { SetTaskStatusCalled = true; return Task.CompletedTask; }
    }

    // ================= Team =================

    [Fact]
    public async Task Team_GetTeamMembers_WithoutViewPermission_ReturnsForbid()
    {
        // ContractManager/QAManager هیچ Grant ای روی "Team" ندارند - حتی View
        var (ctx, perms, user) = await CreateInfra(nameof(Team_GetTeamMembers_WithoutViewPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.GetTeamMembers(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_GetTeamMembers_DisciplineEngineer_HasViewOnly_CanRead()
    {
        // DisciplineEngineer باید بتواند این AJAX را برای بازبینی WorkOrder فراخوانی کند (View)،
        // حتی اگرچه اجازه‌ی مدیریت ترکیب تیم (Edit) را ندارد.
        var (ctx, perms, user) = await CreateInfra(nameof(Team_GetTeamMembers_DisciplineEngineer_HasViewOnly_CanRead), role: SystemRoles.DisciplineEngineer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.GetTeamMembers(1);

        Assert.IsType<JsonResult>(result);
    }

    // ================= Crew =================

    [Fact]
    public async Task Crew_CreateCrew_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Crew_CreateCrew_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.Viewer);
        var service = new FakeCrewService();
        var controller = new CrewController(ctx, service, user, perms).WithTempData();

        var result = await controller.CreateCrew("C1", "Crew 1", null);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task Crew_CreateCrew_WithPermission_Creates()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Crew_CreateCrew_WithPermission_Creates), role: SystemRoles.MaintenanceEngineer);
        var service = new FakeCrewService();
        var controller = new CrewController(ctx, service, user, perms).WithTempData();

        var result = await controller.CreateCrew("C1", "Crew 1", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(service.CreateCalled);
    }

    [Fact]
    public async Task Crew_AddMember_WithoutPermission_ReturnsForbid_DoesNotAdd()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Crew_AddMember_WithoutPermission_ReturnsForbid_DoesNotAdd), role: SystemRoles.DisciplineEngineer);
        var service = new FakeCrewService();
        var controller = new CrewController(ctx, service, user, perms).WithTempData();

        var result = await controller.AddMember(1, 1, 1);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.AddOrReactivateCalled);
    }

    [Fact]
    public async Task Crew_RemoveMember_WithoutPermission_ReturnsForbid_DoesNotRemove()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Crew_RemoveMember_WithoutPermission_ReturnsForbid_DoesNotRemove), role: SystemRoles.Viewer);
        var service = new FakeCrewService();
        var controller = new CrewController(ctx, service, user, perms).WithTempData();

        var result = await controller.RemoveMember(1);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.RemoveCalled);
    }

    [Fact]
    public async Task Crew_RemoveMember_WithPermission_Removes()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Crew_RemoveMember_WithPermission_Removes), role: SystemRoles.EngineeringManager);
        var service = new FakeCrewService();
        var controller = new CrewController(ctx, service, user, perms).WithTempData();

        var result = await controller.RemoveMember(1);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(service.RemoveCalled);
    }

    [Fact]
    public async Task Crew_Index_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Crew_Index_WithoutViewPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = new CrewController(ctx, new FakeCrewService(), user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    // ================= Inventory Master Data =================

    // ================= Maintenance Master Data =================

    [Fact]
    public async Task MaintenanceMasterData_CreateGroup_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_CreateGroup_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.Viewer);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.CreateGroup("G1", "Group 1", 1, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaintenanceGroups);
    }

    [Fact]
    public async Task MaintenanceMasterData_CreateGroup_WithPermission_Creates()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_CreateGroup_WithPermission_Creates), role: SystemRoles.EngineeringSupervisor);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.CreateGroup("G1", "Group 1", 1, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.MaintenanceGroups);
    }

    [Fact]
    public async Task MaintenanceMasterData_ToggleGroupActive_WithoutPermission_ReturnsForbid_DoesNotToggle()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_ToggleGroupActive_WithoutPermission_ReturnsForbid_DoesNotToggle), role: SystemRoles.Viewer);
        ctx.MaintenanceGroups.Add(new MaintenanceGroup { MaintenanceGroupId = 1, Code = "G1", Name = "Group 1", IsActive = true });
        await ctx.SaveChangesAsync();
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.ToggleGroupActive(1);

        Assert.IsType<ForbidResult>(result);
        Assert.True(ctx.MaintenanceGroups.Single().IsActive);
    }

    [Fact]
    public async Task MaintenanceMasterData_CreateSkill_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_CreateSkill_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.ContractManager);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.CreateSkill("Skill 1", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Skills);
    }

    [Fact]
    public async Task MaintenanceMasterData_CreateQualification_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_CreateQualification_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.QAManager);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.CreateQualification("Qual 1", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Qualifications);
    }

    [Fact]
    public async Task MaintenanceMasterData_CreateCertification_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_CreateCertification_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.DocumentController);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.CreateCertification("Cert 1", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Certifications);
    }

    [Fact]
    public async Task MaintenanceMasterData_Index_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_Index_WithoutViewPermission_ReturnsForbid), role: null);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    // ================= Dynamic Maintenance Organization (Team nesting / Position / Employee Assignment) =================
    // طبق «PEMS Dynamic Maintenance Organization»: همون الگوی دقیق بالا - فقط Permission، نه
    // Scope دیسیپلینی (که با SystemAdmin دور زده می‌شود؛ Scope واقعی تست نمی‌شود این‌جا).

    [Fact]
    public async Task MaintenanceMasterData_CreateSubGroup_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_CreateSubGroup_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.Viewer);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.CreateSubGroup("SUB", "Sub Group", 1, 1, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaintenanceGroups);
    }

    [Fact]
    public async Task MaintenanceMasterData_MoveGroup_WithoutPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(MaintenanceMasterData_MoveGroup_WithoutPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var controller = new MaintenanceMasterDataController(ctx, user, perms, new OrganizationStructureService(ctx)).WithTempData();

        var result = await controller.MoveGroup(1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_OrganizationTree_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_OrganizationTree_WithoutViewPermission_ReturnsForbid), role: null);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.OrganizationTree(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_OrganizationChart_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_OrganizationChart_WithoutViewPermission_ReturnsForbid), role: null);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.OrganizationChart(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_OrganizationChart_WithViewPermission_ReturnsView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_OrganizationChart_WithViewPermission_ReturnsView), role: SystemRoles.SystemAdmin);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.OrganizationChart(null);

        Assert.IsType<ViewResult>(result);
    }

    // ================= Phase 6B: Team Details / Workforce Management =================

    private static async Task<(Discipline Discipline, MaintenanceGroup Group, Team Team)> SeedTeamAsync(PEMSDbContext ctx)
    {
        var discipline = new Discipline { Code = "MECH", Name = "Mechanical", IsActive = true, CreatedDate = DateTime.Now };
        ctx.Disciplines.Add(discipline);
        await ctx.SaveChangesAsync();
        var group = new MaintenanceGroup { Code = "G1", Name = "Group 1", DisciplineId = discipline.DisciplineId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.MaintenanceGroups.Add(group);
        await ctx.SaveChangesAsync();
        var team = new Team { Code = "T1", Name = "Team 1", MaintenanceGroupId = group.MaintenanceGroupId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.Teams.Add(team);
        await ctx.SaveChangesAsync();
        return (discipline, group, team);
    }

}
