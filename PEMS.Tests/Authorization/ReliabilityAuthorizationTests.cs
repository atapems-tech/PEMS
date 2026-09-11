using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 5 — Final Reliability Intelligence» بند ۴۶: ReliabilityController پیش از
/// این فاز هیچ بررسی مجوزی نداشت (خلأ امنیتی واقعی، طبق اصل «Hidden Navigation != Security»
/// همین پروژه که از همون تسک اول این جلسه تثبیت شد) - این تست‌ها اثبات می‌کنند گیت جدید واقعاً
/// کار می‌کند.
/// </summary>
public class ReliabilityAuthorizationTests
{
    private static async Task<(PEMSDbContext Ctx, IPermissionService Permissions, ICurrentUserService CurrentUser)>
        CreateInfra(string dbName, string? role)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ReliabilityAuth_{dbName}");
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

    private static ReliabilityController CreateController(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms)
    {
        var reliabilityService = new ReliabilityService(ctx);
        var intelligenceService = new ReliabilityIntelligenceService(ctx);
        var ramWeibullService = new RamWeibullService(ctx, reliabilityService);
        var predictiveMaintenanceService = new PredictiveMaintenanceService(ctx);
        var conditionMonitoringService = new ConditionMonitoringService(ctx);
        var analyticsService = new ReliabilityAnalyticsService(ctx, reliabilityService);
        var rcmFmeaReadinessService = new RcmFmeaReadinessService(ctx);

        return new ReliabilityController(reliabilityService, analyticsService,
            new ReliabilityDataQualityService(ctx), intelligenceService, new RcmFeedbackService(ctx), user, perms,
            ramWeibullService, new ReliabilitySimulationService(ramWeibullService), conditionMonitoringService,
            predictiveMaintenanceService,
            new AIReliabilityInsightService(ctx, null!, analyticsService, intelligenceService, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            new ClassIntelligenceService(ctx, new EquipmentClassCoverageService(ctx), analyticsService, intelligenceService, rcmFmeaReadinessService),
            new EquipmentIntelligenceService(ctx, reliabilityService, analyticsService, ramWeibullService, rcmFmeaReadinessService, predictiveMaintenanceService, conditionMonitoringService),
            ctx)
            .WithTempData();
    }

    [Fact]
    public async Task Index_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Index(null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Index_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Index(null, null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task DataQuality_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(DataQuality_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.DataQuality();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DataQuality_MaintenanceEngineer_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(DataQuality_MaintenanceEngineer_CanView), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.DataQuality();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ClassDetail_UnknownClass_ReturnsNotFound_ForAuthorizedUser()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ClassDetail_UnknownClass_ReturnsNotFound_ForAuthorizedUser), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ClassDetail(99999);

        Assert.IsType<NotFoundResult>(result);
    }

    // ==============================================================================
    // طبق «PEMS Phase 7» بند ۲۸: همان ماژول "Reliability" موجود - View برای Intelligence/
    // RcmFeedback، Edit برای GenerateRcmEvidence، Approve فقط برای ReviewRcmRecommendation.
    // ==============================================================================

    [Fact]
    public async Task Intelligence_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Intelligence_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Intelligence(null, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Intelligence_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Intelligence_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Intelligence(null, null, null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task RcmFeedback_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(RcmFeedback_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.RcmFeedback(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RcmFeedback_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(RcmFeedback_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.RcmFeedback(null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task GenerateRcmEvidence_ViewerRole_ReturnsForbid()
    {
        // Viewer فقط View دارد، Edit ندارد - تولید شواهد یک عملیات نوشتنی است.
        var (ctx, perms, user) = await CreateInfra(nameof(GenerateRcmEvidence_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.GenerateRcmEvidence();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GenerateRcmEvidence_MaintenanceEngineer_CanGenerate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(GenerateRcmEvidence_MaintenanceEngineer_CanGenerate), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.GenerateRcmEvidence();

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task ReviewRcmRecommendation_MaintenanceEngineer_ReturnsForbid()
    {
        // Editor (MaintenanceEngineer) می‌تواند شواهد تولید کند اما نمی‌تواند بازبینی/تایید کند -
        // دقیقاً همان تفکیک Editor/Approver که در سراسر این پروژه (RCMFMEA، Reliability) رعایت شده.
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewRcmRecommendation_MaintenanceEngineer_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ReviewRcmRecommendation(1, true, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ReviewRcmRecommendation_EngineeringSupervisor_CanReview()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewRcmRecommendation_EngineeringSupervisor_CanReview), role: SystemRoles.EngineeringSupervisor);
        var controller = CreateController(ctx, user, perms);

        // شناسه‌ی ناموجود عمداً استفاده شد - هدف این تست فقط اثبات عبور از گیت مجوز است، نه منطق سرویس
        var result = await controller.ReviewRcmRecommendation(99999, true, null);

        Assert.IsType<RedirectToActionResult>(result);
    }

    // ==============================================================================
    // طبق «PEMS Phase 9 — Enterprise Reliability, RAM & Predictive Maintenance Intelligence»
    // بند ۲۳: همان ماژول مجوز "Reliability" برای اکشن‌های جدید - این تست‌ها اثبات می‌کنند
    // گیت واقعاً روی هر اکشن جدید هم اعمال شده، نه فقط اکشن‌های قدیمی‌تر.
    // ==============================================================================

    [Fact]
    public async Task Dashboard_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Dashboard_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Dashboard();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Dashboard_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Dashboard_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Dashboard();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task EquipmentProfile_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(EquipmentProfile_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.EquipmentProfile(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ClassIntelligence_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ClassIntelligence_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ClassIntelligence(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Ram_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(Ram_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.Ram(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ConditionMonitoring_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ConditionMonitoring_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ConditionMonitoring(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ConditionMonitoring_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ConditionMonitoring_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ConditionMonitoring(null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateConditionMonitoringPoint_ViewerRole_CannotCreate_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateConditionMonitoringPoint_ViewerRole_CannotCreate_ReturnsForbid), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.CreateConditionMonitoringPoint(1, "Vibration", "VT-1", null, null, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateConditionMonitoringPoint_MaintenanceEngineer_CanCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateConditionMonitoringPoint_MaintenanceEngineer_CanCreate), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(ctx, user, perms);

        // شناسه‌ی ناموجود عمداً استفاده شد - هدف این تست فقط اثبات عبور از گیت مجوز است
        var result = await controller.CreateConditionMonitoringPoint(99999, "Vibration", "VT-1", null, null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task PredictiveMaintenance_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(PredictiveMaintenance_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.PredictiveMaintenance();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task PredictiveMaintenance_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(PredictiveMaintenance_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.PredictiveMaintenance();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task AiInsights_NoPermission_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(AiInsights_NoPermission_ReturnsForbid), role: SystemRoles.QAManager);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.AiInsights(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AiInsights_ViewerRole_CanView()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(AiInsights_ViewerRole_CanView), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.AiInsights(null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task GenerateAiInsights_ViewerRole_CannotGenerate_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(GenerateAiInsights_ViewerRole_CannotGenerate_ReturnsForbid), role: SystemRoles.Viewer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.GenerateAiInsights(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ReviewAiInsight_MaintenanceEngineer_CannotApprove_ReturnsForbid()
    {
        // طبق بند ۱۷: بازبینی/تایید یک بینش AI باید همان سطح مجوز Approve را بخواهد -
        // دقیقاً مثل ReviewRcmRecommendation (نه فقط Edit).
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewAiInsight_MaintenanceEngineer_CannotApprove_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ReviewAiInsight(1, true, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ReviewAiInsight_EngineeringSupervisor_CanReview()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewAiInsight_EngineeringSupervisor_CanReview), role: SystemRoles.EngineeringSupervisor);
        var controller = CreateController(ctx, user, perms);

        var result = await controller.ReviewAiInsight(99999, true, null);

        Assert.IsType<RedirectToActionResult>(result);
    }
}
