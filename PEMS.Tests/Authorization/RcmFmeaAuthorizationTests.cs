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

    // طبق «PEMS Phase 6» بند ۱۳: این تست‌ها فقط مسیر مجوز را می‌سنجند - یک Fake ساده کافی است.
    private class FakeNumberingService : INumberingService
    {
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-0001");
    }

    private static RcmFmeaController CreateController(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms) =>
        new RcmFmeaController(new RcmFmeaReadinessService(ctx), new FMEAService(ctx), new RCMService(ctx),
            new RcmOperationalBridgeService(ctx, new FakeNumberingService()), ctx, user, perms).WithTempData();

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

    [Fact]
    public async Task ApplyRcmDecision_EditorRole_ReturnsForbid_NeverAppliesToOperationalData()
    {
        // طبق «PEMS Phase 6» بند ۱۳/۵۳: اعمال یک تصمیم روی داده‌ی عملیاتی واقعی، ادامه‌ی همان
        // اختیار Approve است - Editor (بدون CanApprove) هرگز نباید بتواند این پل را فعال کند.
        var (ctx, perms, editorUser) = await CreateInfra(nameof(ApplyRcmDecision_EditorRole_ReturnsForbid_NeverAppliesToOperationalData), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await FailureCauseSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureEffectSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureConsequenceSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureModeSeeder.SeedAsync(services.BuildServiceProvider());
        var failureMode = await ctx.FailureModes.FirstAsync();

        var equipment = new Equipment { TagNumber = "EQ-AUTH-1", Name = "EQ-AUTH-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(classId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(classId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, null, "Operational");
        await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        var controller = CreateController(ctx, editorUser, perms);
        var result = await controller.ApplyRcmDecision(classId, decisionId, equipment.EquipmentId);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task ApplyRcmDecision_EngineeringSupervisor_CanApply()
    {
        var (ctx, perms, editorUser) = await CreateInfra(nameof(ApplyRcmDecision_EngineeringSupervisor_CanApply), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await FailureCauseSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureEffectSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureConsequenceSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureModeSeeder.SeedAsync(services.BuildServiceProvider());
        var failureMode = await ctx.FailureModes.FirstAsync();

        var equipment = new Equipment { TagNumber = "EQ-AUTH-2", Name = "EQ-AUTH-2", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(classId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(classId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, null, "Operational");
        await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApplyRcmDecision_EngineeringSupervisor_CanApply) + "_sup", role: SystemRoles.EngineeringSupervisor);
        var supervisorController = CreateController(ctx, supervisorUser, perms);

        var result = await supervisorController.ApplyRcmDecision(classId, decisionId, equipment.EquipmentId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.MaintenanceStrategies);
    }

    // ==============================================================================
    // طبق «PEMS Phase 6.1» بند ۲۲: پوشش مسیر Controller (نه فقط خودِ Service) برای سه حالت رد -
    // تصمیم تاییدنشده، عدم تطابق کلاس تجهیز، و اعمال تکراری - چون این‌ها همان چیزهایی هستند که
    // UI جدید (Modal داخل ClassDetail) واقعاً از طریق همین Action صدا می‌زند.
    // ==============================================================================

    private static async Task<(PEMSDbContext Ctx, IPermissionService Perms, int ClassId, int DecisionId, Equipment Equipment)>
        SeedApprovableDecisionAsync(string dbName, bool approve)
    {
        var (ctx, perms, editorUser) = await CreateInfra(dbName, role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await FailureCauseSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureEffectSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureConsequenceSeeder.SeedAsync(services.BuildServiceProvider());
        await FailureModeSeeder.SeedAsync(services.BuildServiceProvider());
        var failureMode = await ctx.FailureModes.FirstAsync();

        var equipment = new Equipment { TagNumber = $"EQ-{dbName}", Name = $"EQ-{dbName}", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(classId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(classId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, null, "Operational");
        if (approve) await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        return (ctx, perms, classId, decisionId, equipment);
    }

    [Fact]
    public async Task ApplyRcmDecision_NonApprovedDecision_Rejected_NoOperationalDataCreated()
    {
        var (ctx, perms, classId, decisionId, equipment) = await SeedApprovableDecisionAsync(
            nameof(ApplyRcmDecision_NonApprovedDecision_Rejected_NoOperationalDataCreated), approve: false);

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApplyRcmDecision_NonApprovedDecision_Rejected_NoOperationalDataCreated) + "_sup", role: SystemRoles.EngineeringSupervisor);
        var controller = CreateController(ctx, supervisorUser, perms);

        var result = await controller.ApplyRcmDecision(classId, decisionId, equipment.EquipmentId);

        Assert.IsType<RedirectToActionResult>(result); // نه Forbid - این یک رد کسب‌وکاری است، نه مجوزی
        Assert.NotNull(controller.TempData["Error"]);
        Assert.Empty(ctx.MaintenanceStrategies);
        Assert.Empty(ctx.MaintenancePlans);
    }

    [Fact]
    public async Task ApplyRcmDecision_WrongEquipmentClass_Rejected_NoOperationalDataCreated()
    {
        var (ctx, perms, classId, decisionId, _) = await SeedApprovableDecisionAsync(
            nameof(ApplyRcmDecision_WrongEquipmentClass_Rejected_NoOperationalDataCreated), approve: true);

        var motorClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "MOTOR");
        var wrongClassEquipment = new Equipment { TagNumber = "EQ-WRONGCLASS", Name = "EQ-WRONGCLASS", EquipmentType = "Motor", IsActive = true, EquipmentClassId = motorClass.EquipmentClassId };
        ctx.Equipments.Add(wrongClassEquipment);
        await ctx.SaveChangesAsync();

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApplyRcmDecision_WrongEquipmentClass_Rejected_NoOperationalDataCreated) + "_sup", role: SystemRoles.EngineeringSupervisor);
        var controller = CreateController(ctx, supervisorUser, perms);

        var result = await controller.ApplyRcmDecision(classId, decisionId, wrongClassEquipment.EquipmentId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["Error"]);
        Assert.Empty(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task ApplyRcmDecision_AppliedTwice_NoDuplicateStrategyOrPlan()
    {
        var (ctx, perms, classId, decisionId, equipment) = await SeedApprovableDecisionAsync(
            nameof(ApplyRcmDecision_AppliedTwice_NoDuplicateStrategyOrPlan), approve: true);

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApplyRcmDecision_AppliedTwice_NoDuplicateStrategyOrPlan) + "_sup", role: SystemRoles.EngineeringSupervisor);

        var firstController = CreateController(ctx, supervisorUser, perms);
        var firstResult = await firstController.ApplyRcmDecision(classId, decisionId, equipment.EquipmentId);
        var secondController = CreateController(ctx, supervisorUser, perms);
        var secondResult = await secondController.ApplyRcmDecision(classId, decisionId, equipment.EquipmentId);

        Assert.IsType<RedirectToActionResult>(firstResult);
        Assert.IsType<RedirectToActionResult>(secondResult);
        Assert.Null(secondController.TempData["Error"]); // اعمال دوباره خطا نیست - بازاستفاده‌ی امن است
        Assert.Single(ctx.MaintenanceStrategies); // هرگز رکورد تکراری ساخته نشد
        Assert.Empty(ctx.MaintenancePlans); // این تصمیم PMTemplate توصیه نکرده بود
    }

    [Fact]
    public void ApplyRcmDecision_Action_IsAntiForgeryProtected()
    {
        // طبق «PEMS Phase 6.1» بند ۱۱/۲۲: این Action یک POST با تاثیر واقعی روی داده‌ی عملیاتی
        // است - باید دقیقاً مثل بقیه‌ی Action های POST این کنترلر، توسط ValidateAntiForgeryToken
        // محافظت شود.
        var method = typeof(RcmFmeaController).GetMethod(nameof(RcmFmeaController.ApplyRcmDecision));
        Assert.NotNull(method);
        Assert.True(method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute), inherit: true).Any());
        Assert.True(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), inherit: true).Any());
    }
}
