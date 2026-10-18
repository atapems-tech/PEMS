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
    public async Task Team_CreateTeam_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_CreateTeam_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.Viewer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.CreateTeam("T1", "Team 1", 1, null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Teams);
    }

    [Fact]
    public async Task Team_CreateTeam_WithPermission_Creates()
    {
        // طبق «PEMS — P1 Object-Level Authorization & Data Scope Hardening»: CreateTeam حالا
        // Scope دیسیپلین را هم بررسی می‌کند - این تست فقط سطح Permission را می‌سنجد (بررسی
        // Scope جداگانه و کامل در TeamObjectScopeAuthorizationTests.cs پوشش دارد)، پس با
        // SystemAdmin (معاف از Scope) اجرا می‌شود تا به Employee/Discipline نیاز نداشته باشد.
        var (ctx, perms, user) = await CreateInfra(nameof(Team_CreateTeam_WithPermission_Creates), role: SystemRoles.SystemAdmin);
        ctx.MaintenanceGroups.Add(new MaintenanceGroup { MaintenanceGroupId = 1, Code = "G1", Name = "G1", IsActive = true });
        await ctx.SaveChangesAsync();
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.CreateTeam("T1", "Team 1", 1, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.Teams);
    }

    [Fact]
    public async Task Team_AddMember_WithoutPermission_ReturnsForbid_DoesNotAdd()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_AddMember_WithoutPermission_ReturnsForbid_DoesNotAdd), role: SystemRoles.Viewer);
        var service = new FakeTeamService();
        var controller = new TeamController(ctx, service, new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.AddMember(1, 1, 1);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.AddOrReactivateCalled);
    }

    [Fact]
    public async Task Team_AddMember_WithPermission_Adds()
    {
        // همون دلیل بالا: این تست فقط Permission را می‌سنجد، نه Scope (که جداگانه در
        // TeamObjectScopeAuthorizationTests.cs پوشش کامل دارد) - با SystemAdmin اجرا می‌شود.
        var (ctx, perms, user) = await CreateInfra(nameof(Team_AddMember_WithPermission_Adds), role: SystemRoles.SystemAdmin);
        var service = new FakeTeamService();
        var controller = new TeamController(ctx, service, new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.AddMember(1, 1, 1);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(service.AddOrReactivateCalled);
    }

    [Fact]
    public async Task Team_RemoveMember_WithoutPermission_ReturnsForbid_DoesNotRemove()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_RemoveMember_WithoutPermission_ReturnsForbid_DoesNotRemove), role: SystemRoles.DisciplineEngineer);
        var service = new FakeTeamService();
        var controller = new TeamController(ctx, service, new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.RemoveMember(1);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.RemoveCalled);
    }

    [Fact]
    public async Task Team_MoveMember_WithoutPermission_ReturnsForbid_DoesNotMove()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_MoveMember_WithoutPermission_ReturnsForbid_DoesNotMove), role: SystemRoles.ContractManager);
        var service = new FakeTeamService();
        var controller = new TeamController(ctx, service, new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.MoveMember(1, 1, 2, 1);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.MoveCalled);
    }

    [Fact]
    public async Task Team_Index_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_Index_WithoutViewPermission_ReturnsForbid), role: SystemRoles.ContractManager);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

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

    // ================= Discipline (reuses the existing "DisciplineWorkspace" module) =================

    [Fact]
    public async Task Discipline_AddTask_WithoutPermission_ReturnsForbid_DoesNotAdd()
    {
        // MaintenanceEngineer هیچ Grant ای روی "DisciplineWorkspace" ندارد
        var (ctx, perms, user) = await CreateInfra(nameof(Discipline_AddTask_WithoutPermission_ReturnsForbid_DoesNotAdd), role: SystemRoles.MaintenanceEngineer);
        var service = new FakeDisciplineWorkspaceService();
        var controller = new DisciplineController(service, ctx, user, perms).WithTempData();

        var result = await controller.AddTask(1, null, "Title", null, "Action", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.AddTaskCalled);
    }

    [Fact]
    public async Task Discipline_AddTask_WithPermission_Adds()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Discipline_AddTask_WithPermission_Adds), role: SystemRoles.DisciplineEngineer);
        var service = new FakeDisciplineWorkspaceService();
        var controller = new DisciplineController(service, ctx, user, perms).WithTempData();

        var result = await controller.AddTask(1, null, "Title", null, "Action", null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(service.AddTaskCalled);
    }

    [Fact]
    public async Task Discipline_SetTaskStatus_WithoutPermission_ReturnsForbid_DoesNotChangeStatus()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Discipline_SetTaskStatus_WithoutPermission_ReturnsForbid_DoesNotChangeStatus), role: SystemRoles.ContractManager);
        var service = new FakeDisciplineWorkspaceService();
        var controller = new DisciplineController(service, ctx, user, perms).WithTempData();

        var result = await controller.SetTaskStatus(1, 1, "Done");

        Assert.IsType<ForbidResult>(result);
        Assert.False(service.SetTaskStatusCalled);
    }

    [Fact]
    public async Task Discipline_SetTaskStatus_WithPermission_ChangesStatus()
    {
        // طبق ممیزی این پاس: EngineeringManager قبلاً هیچ Grant ای روی DisciplineWorkspace
        // نداشت با اینکه سایدبار «Disciplines» را می‌بیند - این تست همون رفع را اثبات می‌کند.
        var (ctx, perms, user) = await CreateInfra(nameof(Discipline_SetTaskStatus_WithPermission_ChangesStatus), role: SystemRoles.EngineeringManager);
        var service = new FakeDisciplineWorkspaceService();
        var controller = new DisciplineController(service, ctx, user, perms).WithTempData();

        var result = await controller.SetTaskStatus(1, 1, "Done");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(service.SetTaskStatusCalled);
    }

    [Fact]
    public async Task Discipline_Index_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Discipline_Index_WithoutViewPermission_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);
        var controller = new DisciplineController(new FakeDisciplineWorkspaceService(), ctx, user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Discipline_Index_DocumentController_HasViewOnly_CanRead()
    {
        // طبق ممیزی این پاس: DocumentController قبلاً هیچ Grant ای نداشت با اینکه سایدبار را
        // می‌بیند - این تست همون رفع را اثبات می‌کند (فقط View، بدون Create/Edit).
        var (ctx, perms, user) = await CreateInfra(nameof(Discipline_Index_DocumentController_HasViewOnly_CanRead), role: SystemRoles.DocumentController);
        var service = new FakeDisciplineWorkspaceService();
        var controller = new DisciplineController(service, ctx, user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    // ================= Inventory Master Data =================

    [Fact]
    public async Task InventoryMasterData_CreateCategory_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_CreateCategory_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.Viewer);
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.CreateCategory("C1", "Cat 1", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaterialCategories);
    }

    [Fact]
    public async Task InventoryMasterData_CreateCategory_WithPermission_Creates()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_CreateCategory_WithPermission_Creates), role: SystemRoles.MaintenanceEngineer);
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.CreateCategory("C1", "Cat 1", null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.MaterialCategories);
    }

    [Fact]
    public async Task InventoryMasterData_ToggleCategoryActive_WithoutPermission_ReturnsForbid_DoesNotToggle()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_ToggleCategoryActive_WithoutPermission_ReturnsForbid_DoesNotToggle), role: SystemRoles.Viewer);
        ctx.MaterialCategories.Add(new MaterialCategory { MaterialCategoryId = 1, Code = "C1", Name = "Cat 1", IsActive = true });
        await ctx.SaveChangesAsync();
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.ToggleCategoryActive(1);

        Assert.IsType<ForbidResult>(result);
        Assert.True(ctx.MaterialCategories.Single().IsActive);
    }

    [Fact]
    public async Task InventoryMasterData_CreateUom_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_CreateUom_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.ContractManager);
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.CreateUom("U1", "Unit 1", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaterialUOMs);
    }

    [Fact]
    public async Task InventoryMasterData_CreateWarehouse_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_CreateWarehouse_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.QAManager);
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.CreateWarehouse("W1", "WH 1", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Warehouses);
    }

    [Fact]
    public async Task InventoryMasterData_CreateStorageLocation_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_CreateStorageLocation_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.DisciplineEngineer);
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.CreateStorageLocation(1, "L1", "Loc 1", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.StorageLocations);
    }

    [Fact]
    public async Task InventoryMasterData_Index_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(InventoryMasterData_Index_WithoutViewPermission_ReturnsForbid), role: null);
        var controller = new InventoryMasterDataController(ctx, user, perms).WithTempData();

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

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
    public async Task Team_CreateSubTeam_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_CreateSubTeam_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.Viewer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.CreateSubTeam(1, null, "SUB", "Sub Team", "SubTeam", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Teams);
    }

    [Fact]
    public async Task Team_CreatePosition_WithoutPermission_ReturnsForbid_DoesNotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_CreatePosition_WithoutPermission_ReturnsForbid_DoesNotCreate), role: SystemRoles.DisciplineEngineer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.CreatePosition(1, "Technician #01", null, 0);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Positions);
    }

    [Fact]
    public async Task Team_AssignEmployeeToPosition_WithoutPermission_ReturnsForbid_DoesNotAssign()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_AssignEmployeeToPosition_WithoutPermission_ReturnsForbid_DoesNotAssign), role: SystemRoles.Viewer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.AssignEmployeeToPosition(1, 1, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.EmployeeAssignments);
    }

    [Fact]
    public async Task Team_RemoveAssignment_WithoutPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_RemoveAssignment_WithoutPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.RemoveAssignment(1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_MoveEmployeeBetweenPositions_WithoutPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_MoveEmployeeBetweenPositions_WithoutPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.MoveEmployeeBetweenPositions(1, 2, null);

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

    [Fact]
    public async Task Team_CreateSubTeam_WithPermission_AsSystemAdmin_Creates()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_CreateSubTeam_WithPermission_AsSystemAdmin_Creates), role: SystemRoles.SystemAdmin);

        var discipline = new Discipline { Code = "MECH", Name = "Mechanical", IsActive = true, CreatedDate = DateTime.Now };
        ctx.Disciplines.Add(discipline);
        await ctx.SaveChangesAsync();
        var group = new MaintenanceGroup { Code = "G1", Name = "Group 1", DisciplineId = discipline.DisciplineId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.MaintenanceGroups.Add(group);
        await ctx.SaveChangesAsync();

        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.CreateSubTeam(group.MaintenanceGroupId, null, "T1", "Team 1", "Team", null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.Teams);
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

    [Fact]
    public async Task Team_TeamDetails_WithoutViewPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_TeamDetails_WithoutViewPermission_ReturnsForbid), role: null);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.TeamDetails(team.TeamId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_TeamDetails_WithViewPermission_ReturnsViewWithModel()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_TeamDetails_WithViewPermission_ReturnsViewWithModel), role: SystemRoles.SystemAdmin);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.TeamDetails(team.TeamId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PEMS.Application.DTOs.OrganizationTeamNodeDto>(view.Model);
        Assert.Equal(team.TeamId, model.TeamId);
    }

    [Fact]
    public async Task Team_TeamDetails_UnknownTeamId_ReturnsNotFound()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_TeamDetails_UnknownTeamId_ReturnsNotFound), role: SystemRoles.SystemAdmin);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.TeamDetails(999999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Team_MoveTeamNode_WithoutPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_MoveTeamNode_WithoutPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.MoveTeamNode(team.TeamId, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_DeactivateTeamNode_WithoutPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_DeactivateTeamNode_WithoutPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.DeactivateTeamNode(team.TeamId);

        Assert.IsType<ForbidResult>(result);

        var reloaded = await ctx.Teams.FindAsync(team.TeamId);
        Assert.True(reloaded!.IsActive);
    }

    [Fact]
    public async Task Team_MovePosition_WithoutPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_MovePosition_WithoutPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var position = new Position { Title = "Technician", TeamId = team.TeamId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.MovePosition(position.PositionId, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Team_DeactivatePosition_WithoutPermission_ReturnsForbid_DoesNotDeactivate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_DeactivatePosition_WithoutPermission_ReturnsForbid_DoesNotDeactivate), role: SystemRoles.Viewer);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var position = new Position { Title = "Technician", TeamId = team.TeamId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.DeactivatePosition(position.PositionId, null);

        Assert.IsType<ForbidResult>(result);
        var reloaded = await ctx.Positions.FindAsync(position.PositionId);
        Assert.True(reloaded!.IsActive);
    }

    [Fact]
    public async Task Team_UpdatePosition_WithoutPermission_ReturnsForbid_DoesNotChange()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_UpdatePosition_WithoutPermission_ReturnsForbid_DoesNotChange), role: SystemRoles.Viewer);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var position = new Position { Title = "Technician", TeamId = team.TeamId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.UpdatePosition(position.PositionId, "Should Not Apply", null, 0);

        Assert.IsType<ForbidResult>(result);
        var reloaded = await ctx.Positions.FindAsync(position.PositionId);
        Assert.Equal("Technician", reloaded!.Title);
    }

    [Fact]
    public async Task Team_UpdatePosition_WithPermission_AsSystemAdmin_Updates()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_UpdatePosition_WithPermission_AsSystemAdmin_Updates), role: SystemRoles.SystemAdmin);
        var (_, _, team) = await SeedTeamAsync(ctx);
        var position = new Position { Title = "Technician", TeamId = team.TeamId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();
        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.UpdatePosition(position.PositionId, "Senior Technician", "desc", 3, team.TeamId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TeamController.TeamDetails), redirect.ActionName);
        var reloaded = await ctx.Positions.FindAsync(position.PositionId);
        Assert.Equal("Senior Technician", reloaded!.Title);
    }

    [Fact]
    public async Task Team_AssignEmployeeToPosition_WithPermission_AndReturnTeamId_RedirectsToTeamDetails()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Team_AssignEmployeeToPosition_WithPermission_AndReturnTeamId_RedirectsToTeamDetails), role: SystemRoles.SystemAdmin);
        var (discipline, _, team) = await SeedTeamAsync(ctx);
        var department = new Department { Code = "D1", Name = "Dept", IsActive = true, CreatedDate = DateTime.Now };
        ctx.Departments.Add(department);
        await ctx.SaveChangesAsync();
        var employee = new Employee { FirstName = "Ali", LastName = "Ahmadi", DepartmentId = department.DepartmentId, DisciplineId = discipline.DisciplineId, IsActive = true };
        ctx.Employees.Add(employee);
        var position = new Position { Title = "Technician", TeamId = team.TeamId, IsActive = true, CreatedDate = DateTime.Now };
        ctx.Positions.Add(position);
        await ctx.SaveChangesAsync();

        var controller = new TeamController(ctx, new FakeTeamService(), new OrganizationStructureService(ctx), user, perms).WithTempData();

        var result = await controller.AssignEmployeeToPosition(position.PositionId, employee.EmployeeId, null, team.TeamId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TeamController.TeamDetails), redirect.ActionName);
        Assert.Equal(team.TeamId, redirect.RouteValues!["teamId"]);
        Assert.Single(ctx.EmployeeAssignments);
    }
}
