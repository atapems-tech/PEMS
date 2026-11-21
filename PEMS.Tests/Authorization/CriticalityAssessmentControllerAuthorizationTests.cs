using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 7.6»: ماژول مجوز مجزای "CriticalityAssessment" - Viewer فقط View، Editor
/// (MaintenanceEngineer) Create/Edit ولی هرگز Approve، Approve فقط Supervisor/Manager.
/// </summary>
public class CriticalityAssessmentControllerAuthorizationTests
{
    private static async Task<(CriticalityAssessmentController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"CritAssessAuth_{dbName}");
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

        var assessmentService = new CriticalityAssessmentService(ctx);
        var controller = new CriticalityAssessmentController(assessmentService, ctx, currentUser, permissionService).WithTempData();
        return (controller, ctx);
    }

    // SCRUM-701: the workflow commands are operations; the host refuses a caller without the operation permission, then calls the operation.
    private static async Task<(bool Allowed, (bool Success, string? Error) Outcome)> RunAsync(string dbName, string role, string permission,
        Func<PEMS.Web.Services.CriticalityAssessmentOperations, Task<(bool Success, string? Error)>> call, PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var permissionService = new PermissionService(ctx, new AccessControlService(ctx));
        var parts = permission.Split('.');
        if (!await permissionService.HasPermissionAsync(new List<string> { role }, parts[0], parts[1])) return (false, default);
        var ops = new PEMS.Web.Services.CriticalityAssessmentOperations(new CriticalityAssessmentService(ctx), new FakeCurrentUserService { Roles = new List<string> { role }, UserName = "tester" });
        return (true, await call(ops));
    }

    [Fact]
    public async Task Dashboard_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Dashboard_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Dashboard();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Dashboard_ViewerRole_CanView()
    {
        var (controller, _) = await CreateController(nameof(Dashboard_ViewerRole_CanView), role: SystemRoles.Viewer);

        var result = await controller.Dashboard();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_NoPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_NoPermission_ReturnsForbid), role: null);

        var result = await controller.Index(null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_ViewerRole_ReturnsForbid_NoAssessmentCreated()
    {
        var (controller, ctx) = await CreateController(nameof(Create_ViewerRole_ReturnsForbid_NoAssessmentCreated), role: SystemRoles.Viewer);
        var equipment = new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var result = await controller.Create(equipment.EquipmentId, "rationale");

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.CriticalityAssessments);
    }

    [Fact]
    public async Task Create_MaintenanceEngineer_CanCreate()
    {
        var (controller, ctx) = await CreateController(nameof(Create_MaintenanceEngineer_CanCreate), role: SystemRoles.MaintenanceEngineer);
        var equipment = new Equipment { TagNumber = "EQ-2", Name = "EQ-2", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var result = await controller.Create(equipment.EquipmentId, "rationale");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.CriticalityAssessments);
    }

    [Fact]
    public async Task Approve_MaintenanceEngineer_ReturnsForbid_StatusUnchanged()
    {
        // طبق بند ۱۱: Editor (MaintenanceEngineer) هرگز نمی‌تواند تایید کند - فقط Supervisor/Manager
        var (controller, ctx) = await CreateController(nameof(Approve_MaintenanceEngineer_ReturnsForbid_StatusUnchanged), role: SystemRoles.MaintenanceEngineer);
        var equipment = new Equipment { TagNumber = "EQ-3", Name = "EQ-3", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment
        {
            EquipmentId = equipment.EquipmentId, Status = "UNDER_REVIEW", PreparedBy = "engineer1",
            SafetyScore = 3, ProductionScore = 3, EnvironmentScore = 3, IsActive = true, CreatedDate = System.DateTime.Now
        };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("approve-eng", SystemRoles.MaintenanceEngineer, "CriticalityAssessment.Approve", o => o.ApproveAsync(assessment.CriticalityAssessmentId), ctx);

        Assert.False(result.Allowed);
        Assert.Equal("UNDER_REVIEW", ctx.CriticalityAssessments.Single().Status);
        Assert.Empty(ctx.EquipmentCriticalities);
    }

    [Fact]
    public async Task Approve_EngineeringSupervisor_DifferentPreparer_CanApprove_PublishesResult()
    {
        var (controller, ctx) = await CreateController(nameof(Approve_EngineeringSupervisor_DifferentPreparer_CanApprove_PublishesResult), role: SystemRoles.EngineeringSupervisor);
        var equipment = new Equipment { TagNumber = "EQ-4", Name = "EQ-4", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment
        {
            EquipmentId = equipment.EquipmentId, Status = "UNDER_REVIEW", PreparedBy = "engineer1",
            SafetyScore = 4, ProductionScore = 3, EnvironmentScore = 2, IsActive = true, CreatedDate = System.DateTime.Now
        };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("approve-sup", SystemRoles.EngineeringSupervisor, "CriticalityAssessment.Approve", o => o.ApproveAsync(assessment.CriticalityAssessmentId), ctx);

        Assert.True(result.Allowed);
        Assert.True(result.Outcome.Success);
        Assert.Equal("APPROVED", ctx.CriticalityAssessments.Single().Status);
        Assert.Single(ctx.EquipmentCriticalities);
    }

    [Fact]
    public async Task Reject_MaintenanceEngineer_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Reject_MaintenanceEngineer_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);
        var equipment = new Equipment { TagNumber = "EQ-5", Name = "EQ-5", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment { EquipmentId = equipment.EquipmentId, Status = "UNDER_REVIEW", IsActive = true, CreatedDate = System.DateTime.Now };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("reject-eng", SystemRoles.MaintenanceEngineer, "CriticalityAssessment.Approve", o => o.RejectAsync(assessment.CriticalityAssessmentId), ctx);

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task SetScores_ViewerRole_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(SetScores_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);
        var equipment = new Equipment { TagNumber = "EQ-6", Name = "EQ-6", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var assessment = new CriticalityAssessment { EquipmentId = equipment.EquipmentId, Status = "DRAFT", IsActive = true, CreatedDate = System.DateTime.Now };
        ctx.CriticalityAssessments.Add(assessment);
        await ctx.SaveChangesAsync();

        var result = await RunAsync("scores-viewer", SystemRoles.Viewer, "CriticalityAssessment.Edit", o => o.SetScoresAsync(assessment.CriticalityAssessmentId, 3, 3, 3, null), ctx);

        Assert.False(result.Allowed);
        Assert.Null(ctx.CriticalityAssessments.Single().SafetyScore);
    }
}
