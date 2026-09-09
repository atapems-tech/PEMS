using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence» بند ۲۰/۳۷/۳۹: ماژول مجوز مجزا
/// "RCMFMEA" - MaintenanceEngineer فقط Draft می‌سازد (Editor)، فقط EngineeringSupervisor/
/// EngineeringManager می‌توانند تایید کنند (Approver). این تست‌ها اثبات می‌کنند یک کاربر
/// فقط‌ویرایش‌گر هرگز نمی‌تواند یک تحلیل مهندسی معتبر را تایید کند.
/// </summary>
public class RcmFmeaAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"RcmFmeaAuth_{dbName}");
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

    private static async Task<int> SeedEquipmentClassAsync(PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await EquipmentClassSeeder.SeedAsync(services.BuildServiceProvider());
        var cls = await ctx.EquipmentClasses.FirstAsync(x => x.Code == "PUMP_CENT");
        return cls.EquipmentClassId;
    }

    private static RcmFmeaController CreateController(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms) =>
        new RcmFmeaController(new RcmFmeaReadinessService(ctx), new FMEAService(ctx), new RCMService(ctx),
            ctx, user, perms).WithTempData();

    [Fact]
    public async Task Index_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateFmeaDraft_MaintenanceEngineer_CanCreate_ButCannotApprove()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateFmeaDraft_MaintenanceEngineer_CanCreate_ButCannotApprove), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);
        var controller = CreateController(ctx, user, perms);

        var createResult = await controller.CreateFmeaDraft(classId, "Test FMEA", null);
        Assert.IsType<RedirectToActionResult>(createResult);

        var fmea = await ctx.FMEAAnalyses.SingleAsync();
        var approveResult = await controller.ApproveFmea(classId, fmea.FMEAAnalysisId);

        Assert.IsType<ForbidResult>(approveResult); // Editor - نه Approver
    }

    [Fact]
    public async Task ApproveFmea_EngineeringSupervisor_CanApprove()
    {
        var (ctx, perms, editorUser) = await CreateInfra(nameof(ApproveFmea_EngineeringSupervisor_CanApprove), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);

        var editorController = CreateController(ctx, editorUser, perms);
        await editorController.CreateFmeaDraft(classId, "Test FMEA", null);
        var fmea = await ctx.FMEAAnalyses.SingleAsync();
        await editorController.SubmitFmea(classId, fmea.FMEAAnalysisId);

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApproveFmea_EngineeringSupervisor_CanApprove) + "_sup", role: SystemRoles.EngineeringSupervisor);
        var supervisorController = CreateController(ctx, supervisorUser, perms);

        var approveResult = await supervisorController.ApproveFmea(classId, fmea.FMEAAnalysisId);

        Assert.IsType<RedirectToActionResult>(approveResult);
        var reloaded = await ctx.FMEAAnalyses.AsNoTracking().SingleAsync();
        Assert.Equal("APPROVED", reloaded.Status);
    }

    [Fact]
    public async Task CreateFmeaDraft_ViewerRole_CannotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateFmeaDraft_ViewerRole_CannotCreate), role: SystemRoles.Viewer);
        var classId = await SeedEquipmentClassAsync(ctx);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.CreateFmeaDraft(classId, "Test FMEA", null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await ctx.FMEAAnalyses.CountAsync());
    }

    [Fact]
    public async Task ApproveRcmDecision_UnauthorizedRole_ReturnsForbid_DecisionRemainsUnapproved()
    {
        var (ctx, perms, editorUser) = await CreateInfra(nameof(ApproveRcmDecision_UnauthorizedRole_ReturnsForbid_DecisionRemainsUnapproved), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);

        // Seed کافی برای ساخت یک FailureMode واقعی
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await FailureCauseSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureEffectSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureConsequenceSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureModeSeeder.SeedAsync(services.BuildServiceProvider());
        var failureMode = await ctx.FailureModes.FirstAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(classId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(classId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, null, "Operational");

        var controller = CreateController(ctx, editorUser, perms);
        var result = await controller.ApproveRcmDecision(classId, decisionId);

        Assert.IsType<ForbidResult>(result);
        var decision = await ctx.RCMDecisions.AsNoTracking().SingleAsync();
        Assert.Equal("DRAFT", decision.Status);
    }
}
