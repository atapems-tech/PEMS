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

    // SCRUM-744: the write commands are operations. The action host checks the operation permission first (Allowed false = refused, nothing runs), then calls the operation.
    private sealed record Outcome(bool Allowed, bool Success, string? Error);

    private sealed class OpsHost(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms)
    {
        private readonly PEMS.Web.Services.RcmFmeaOperations _ops = new(ctx, new FMEAService(ctx), new RCMService(ctx), new RcmOperationalBridgeService(ctx, new FakeNumberingService()), user);

        private async Task<Outcome> Run(string action, Func<PEMS.Web.Services.RcmFmeaOperations, Task<(bool Success, string? Error)>> call)
        {
            var permission = ((PEMS.Application.Common.PemsOperationAttribute)typeof(IRcmFmeaOperations).GetMethod(action + "Async")!
                .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!.Split('.');
            if (!await perms.HasPermissionAsync(user.Roles, permission[0], permission[1])) return new(false, false, null);
            var r = await call(_ops);
            return new(true, r.Success, r.Error);
        }

        public Task<Outcome> CreateFmeaDraft(int classId, string title, string? description) => Run("CreateFmeaDraft", o => o.CreateFmeaDraftAsync(classId, title, description));
        public Task<Outcome> SubmitFmea(int fmeaId) => Run("SubmitFmea", o => o.SubmitFmeaAsync(fmeaId));
        public Task<Outcome> ApproveFmea(int fmeaId) => Run("ApproveFmea", o => o.ApproveFmeaAsync(fmeaId));
        public Task<Outcome> ApproveRcmDecision(int decisionId) => Run("ApproveRcmDecision", o => o.ApproveRcmDecisionAsync(decisionId));
        public Task<Outcome> ApplyRcmDecision(int decisionId, int equipmentId) => Run("ApplyRcmDecision", o => o.ApplyRcmDecisionAsync(decisionId, equipmentId));
    }

    private static OpsHost CreateHost(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms) => new(ctx, user, perms);

    // SCRUM-744: every read of the coverage matrix and the class detail is an operation with RCMFMEA.View (the data host refuses a caller without it).
    [Fact]
    public async Task Reads_NoPermission_AreRefused_ViewerCanView()
    {
        var (_, perms, none) = await CreateInfra(nameof(Reads_NoPermission_AreRefused_ViewerCanView) + "N", role: SystemRoles.QAManager);
        Assert.False(await perms.HasPermissionAsync(none.Roles, "RCMFMEA", "View"));
        var (_, perms2, viewer) = await CreateInfra(nameof(Reads_NoPermission_AreRefused_ViewerCanView) + "V", role: SystemRoles.Viewer);
        Assert.True(await perms2.HasPermissionAsync(viewer.Roles, "RCMFMEA", "View"));
        Assert.Equal("RCMFMEA.View", ((PEMS.Application.Common.PemsOperationAttribute)typeof(PEMS.Application.Interfaces.IRcmFmeaPagesOperations).GetMethod("CoverageAsync")!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission);
    }

    [Fact]
    public async Task CreateFmeaDraft_MaintenanceEngineer_CanCreate_ButCannotApprove()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateFmeaDraft_MaintenanceEngineer_CanCreate_ButCannotApprove), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);
        var host = CreateHost(ctx, user, perms);

        var createResult = await host.CreateFmeaDraft(classId, "Test FMEA", null);
        Assert.True(createResult.Allowed && createResult.Success);

        var fmea = await ctx.FMEAAnalyses.SingleAsync();
        var approveResult = await host.ApproveFmea(fmea.FMEAAnalysisId);

        Assert.False(approveResult.Allowed); // Editor - نه Approver
    }

    [Fact]
    public async Task ApproveFmea_EngineeringSupervisor_CanApprove()
    {
        var (ctx, perms, editorUser) = await CreateInfra(nameof(ApproveFmea_EngineeringSupervisor_CanApprove), role: SystemRoles.MaintenanceEngineer);
        var classId = await SeedEquipmentClassAsync(ctx);

        var editorHost = CreateHost(ctx, editorUser, perms);
        await editorHost.CreateFmeaDraft(classId, "Test FMEA", null);
        var fmea = await ctx.FMEAAnalyses.SingleAsync();
        await editorHost.SubmitFmea(fmea.FMEAAnalysisId);

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApproveFmea_EngineeringSupervisor_CanApprove) + "_sup", role: SystemRoles.EngineeringSupervisor);
        var supervisorHost = CreateHost(ctx, supervisorUser, perms);

        var approveResult = await supervisorHost.ApproveFmea(fmea.FMEAAnalysisId);

        Assert.True(approveResult.Allowed && approveResult.Success);
        var reloaded = await ctx.FMEAAnalyses.AsNoTracking().SingleAsync();
        Assert.Equal("APPROVED", reloaded.Status);
    }

    [Fact]
    public async Task CreateFmeaDraft_ViewerRole_CannotCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateFmeaDraft_ViewerRole_CannotCreate), role: SystemRoles.Viewer);
        var classId = await SeedEquipmentClassAsync(ctx);
        var host = CreateHost(ctx, user, perms);

        var result = await host.CreateFmeaDraft(classId, "Test FMEA", null);

        Assert.False(result.Allowed);
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

        var host = CreateHost(ctx, editorUser, perms);
        var result = await host.ApproveRcmDecision(decisionId);

        Assert.False(result.Allowed);
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

        var host = CreateHost(ctx, editorUser, perms);
        var result = await host.ApplyRcmDecision(decisionId, equipment.EquipmentId);

        Assert.False(result.Allowed);
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
        var supervisorHost = CreateHost(ctx, supervisorUser, perms);

        var result = await supervisorHost.ApplyRcmDecision(decisionId, equipment.EquipmentId);

        Assert.True(result.Allowed && result.Success);
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
        var host = CreateHost(ctx, supervisorUser, perms);

        var result = await host.ApplyRcmDecision(decisionId, equipment.EquipmentId);

        Assert.True(result.Allowed); // نه Forbid - این یک رد کسب‌وکاری است، نه مجوزی
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
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
        var host = CreateHost(ctx, supervisorUser, perms);

        var result = await host.ApplyRcmDecision(decisionId, wrongClassEquipment.EquipmentId);

        Assert.True(result.Allowed);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task ApplyRcmDecision_AppliedTwice_NoDuplicateStrategyOrPlan()
    {
        var (ctx, perms, classId, decisionId, equipment) = await SeedApprovableDecisionAsync(
            nameof(ApplyRcmDecision_AppliedTwice_NoDuplicateStrategyOrPlan), approve: true);

        var (_, _, supervisorUser) = await CreateInfra(nameof(ApplyRcmDecision_AppliedTwice_NoDuplicateStrategyOrPlan) + "_sup", role: SystemRoles.EngineeringSupervisor);

        var firstResult = await CreateHost(ctx, supervisorUser, perms).ApplyRcmDecision(decisionId, equipment.EquipmentId);
        var secondResult = await CreateHost(ctx, supervisorUser, perms).ApplyRcmDecision(decisionId, equipment.EquipmentId);

        Assert.True(firstResult.Allowed && firstResult.Success);
        Assert.True(secondResult.Allowed && secondResult.Success);
        Assert.Null(secondResult.Error); // اعمال دوباره خطا نیست - بازاستفاده‌ی امن است
        Assert.Single(ctx.MaintenanceStrategies); // هرگز رکورد تکراری ساخته نشد
        Assert.Empty(ctx.MaintenancePlans); // این تصمیم PMTemplate توصیه نکرده بود
    }

    [Fact]
    public void ApplyRcmDecision_Operation_NeedsTheApprovePermission()
    {
        // SCRUM-744: the antiforgery check is the action host's for every action definition; what stays here is the gate of the bridge.
        var method = typeof(IRcmFmeaOperations).GetMethod(nameof(IRcmFmeaOperations.ApplyRcmDecisionAsync));
        Assert.NotNull(method);
        var attribute = (PEMS.Application.Common.PemsOperationAttribute)method!.GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0];
        Assert.Equal("RCMFMEA.Approve", attribute.Permission);
    }
}
