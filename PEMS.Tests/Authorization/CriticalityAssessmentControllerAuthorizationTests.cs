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

        var result = await controller.Approve(assessment.CriticalityAssessmentId);

        Assert.IsType<ForbidResult>(result);
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

        var result = await controller.Approve(assessment.CriticalityAssessmentId);

        Assert.IsType<RedirectToActionResult>(result);
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

        var result = await controller.Reject(assessment.CriticalityAssessmentId);

        Assert.IsType<ForbidResult>(result);
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

        var result = await controller.SetScores(assessment.CriticalityAssessmentId, 3, 3, 3, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(ctx.CriticalityAssessments.Single().SafetyScore);
    }
}
