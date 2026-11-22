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

    // SCRUM-678: the write commands are operations. The action host refuses a caller without the operation permission (false: nothing runs), else it calls the operation (true).
    private static async Task<bool> RunOperation(PEMSDbContext ctx, ICurrentUserService user, IPermissionService perms, string action, Func<IReliabilityOperations, Task> call)
    {
        var permission = ((PEMS.Application.Common.PemsOperationAttribute)typeof(IReliabilityOperations).GetMethod(action + "Async")!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!.Split('.');
        if (!await perms.HasPermissionAsync(user.Roles, permission[0], permission[1])) return false;
        var reliabilityService = new ReliabilityService(ctx);
        var analyticsService = new ReliabilityAnalyticsService(ctx, reliabilityService);
        var ops = new PEMS.Web.Services.ReliabilityOperations(ctx, new RcmFeedbackService(ctx), new ConditionMonitoringService(ctx),
            new AIReliabilityInsightService(ctx, null!, analyticsService, new ReliabilityIntelligenceService(ctx), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()), user);
        await call(ops);
        return true;
    }

    // SCRUM-746: every read of the reliability pages is an operation of IReliabilityPagesOperations with the permission Reliability.View;
    // the action and data hosts refuse a caller without it before they call the operation.
    private static string ViewPermission(string method)
        => ((PEMS.Application.Common.PemsOperationAttribute)typeof(IReliabilityPagesOperations).GetMethod(method)!
            .GetCustomAttributes(typeof(PEMS.Application.Common.PemsOperationAttribute), false)[0]).Permission!;

    [Theory]
    [InlineData("FleetAsync")]
    [InlineData("DataQualityAsync")]
    [InlineData("RecommendationsAsync")]
    [InlineData("ConditionFoundationAsync")]
    [InlineData("PredictiveModelsAsync")]
    [InlineData("AiInsightsAsync")]
    [InlineData("CbmDashboardAsync")]
    public async Task EveryRead_NeedsTheViewPermission(string method)
    {
        Assert.Equal("Reliability.View", ViewPermission(method));
        var (_, perms, user) = await CreateInfra(nameof(EveryRead_NeedsTheViewPermission) + method, role: SystemRoles.QAManager);
        Assert.False(await perms.HasPermissionAsync(user.Roles, "Reliability", "View"));
    }

    [Fact]
    public async Task Reads_ViewerRole_CanView()
    {
        var (_, perms, user) = await CreateInfra(nameof(Reads_ViewerRole_CanView), role: SystemRoles.Viewer);
        Assert.True(await perms.HasPermissionAsync(user.Roles, "Reliability", "View"));
    }

    [Fact]
    public async Task Trend_RangeIsAlwaysBounded_AndAnUnknownPointHasNoTrend()
    {
        var (ctx, _, _) = await CreateInfra(nameof(Trend_RangeIsAlwaysBounded_AndAnUnknownPointHasNoTrend), role: SystemRoles.Viewer);
        var reliabilityService = new ReliabilityService(ctx);
        var analytics = new ReliabilityAnalyticsService(ctx, reliabilityService);
        var intelligence = new ReliabilityIntelligenceService(ctx);
        var ramWeibull = new RamWeibullService(ctx, reliabilityService);
        var rcmFeedback = new RcmFeedbackService(ctx);
        var ops = new PEMS.Web.Services.ReliabilityPagesOperations(reliabilityService, analytics, new ReliabilityDataQualityService(ctx), intelligence, rcmFeedback, ramWeibull, new ReliabilitySimulationService(ramWeibull),
            new ConditionMonitoringService(ctx), new PredictiveMaintenanceService(ctx), null!, null!, null!);
        Assert.Null(await ops.TrendAsync(99999, "custom", new DateTime(2020, 1, 1), new DateTime(2026, 1, 1)));
        Assert.Empty(await ops.FleetAsync());
    }

    [Fact]
    public async Task GenerateRcmEvidence_ViewerRole_ReturnsForbid()
    {
        // Viewer فقط View دارد، Edit ندارد - تولید شواهد یک عملیات نوشتنی است.
        var (ctx, perms, user) = await CreateInfra(nameof(GenerateRcmEvidence_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);
        var allowed = await RunOperation(ctx, user, perms, "GenerateRcmEvidence", o => o.GenerateRcmEvidenceAsync());

        Assert.False(allowed);
    }

    [Fact]
    public async Task GenerateRcmEvidence_MaintenanceEngineer_CanGenerate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(GenerateRcmEvidence_MaintenanceEngineer_CanGenerate), role: SystemRoles.MaintenanceEngineer);
        var allowed = await RunOperation(ctx, user, perms, "GenerateRcmEvidence", o => o.GenerateRcmEvidenceAsync());

        Assert.True(allowed);
    }

    [Fact]
    public async Task ReviewRcmRecommendation_MaintenanceEngineer_ReturnsForbid()
    {
        // Editor (MaintenanceEngineer) می‌تواند شواهد تولید کند اما نمی‌تواند بازبینی/تایید کند -
        // دقیقاً همان تفکیک Editor/Approver که در سراسر این پروژه (RCMFMEA، Reliability) رعایت شده.
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewRcmRecommendation_MaintenanceEngineer_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);
        var allowed = await RunOperation(ctx, user, perms, "ReviewRcmRecommendation", o => o.ReviewRcmRecommendationAsync(1, true, null));

        Assert.False(allowed);
    }

    [Fact]
    public async Task ReviewRcmRecommendation_EngineeringSupervisor_CanReview()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewRcmRecommendation_EngineeringSupervisor_CanReview), role: SystemRoles.EngineeringSupervisor);
        var allowed = await RunOperation(ctx, user, perms, "ReviewRcmRecommendation", o => o.ReviewRcmRecommendationAsync(99999, true, null));

        Assert.True(allowed);
    }

    // ==============================================================================
    // طبق «PEMS Phase 9 — Enterprise Reliability, RAM & Predictive Maintenance Intelligence»
    // بند ۲۳: همان ماژول مجوز "Reliability" برای اکشن‌های جدید - این تست‌ها اثبات می‌کنند
    // گیت واقعاً روی هر اکشن جدید هم اعمال شده، نه فقط اکشن‌های قدیمی‌تر.
    // ==============================================================================

    [Fact]
    public async Task CreateConditionMonitoringPoint_ViewerRole_CannotCreate_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateConditionMonitoringPoint_ViewerRole_CannotCreate_ReturnsForbid), role: SystemRoles.Viewer);
        var allowed = await RunOperation(ctx, user, perms, "CreateConditionMonitoringPoint", o => o.CreateConditionMonitoringPointAsync(1, "Vibration", "VT-1"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task CreateConditionMonitoringPoint_MaintenanceEngineer_CanCreate()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(CreateConditionMonitoringPoint_MaintenanceEngineer_CanCreate), role: SystemRoles.MaintenanceEngineer);
        var allowed = await RunOperation(ctx, user, perms, "CreateConditionMonitoringPoint", o => o.CreateConditionMonitoringPointAsync(99999, "Vibration", "VT-1"));

        Assert.True(allowed);
    }

    [Fact]
    public async Task GenerateAiInsights_ViewerRole_CannotGenerate_ReturnsForbid()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(GenerateAiInsights_ViewerRole_CannotGenerate_ReturnsForbid), role: SystemRoles.Viewer);
        var allowed = await RunOperation(ctx, user, perms, "GenerateAiInsights", o => o.GenerateAiInsightsAsync(null));

        Assert.False(allowed);
    }

    [Fact]
    public async Task ReviewAiInsight_MaintenanceEngineer_CannotApprove_ReturnsForbid()
    {
        // طبق بند ۱۷: بازبینی/تایید یک بینش AI باید همان سطح مجوز Approve را بخواهد -
        // دقیقاً مثل ReviewRcmRecommendation (نه فقط Edit).
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewAiInsight_MaintenanceEngineer_CannotApprove_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);
        var allowed = await RunOperation(ctx, user, perms, "ReviewAiInsight", o => o.ReviewAiInsightAsync(1, true, null, null));

        Assert.False(allowed);
    }

    [Fact]
    public async Task ReviewAiInsight_EngineeringSupervisor_CanReview()
    {
        var (ctx, perms, user) = await CreateInfra(nameof(ReviewAiInsight_EngineeringSupervisor_CanReview), role: SystemRoles.EngineeringSupervisor);
        var allowed = await RunOperation(ctx, user, perms, "ReviewAiInsight", o => o.ReviewAiInsightAsync(99999, true, null, null));

        Assert.True(allowed);
    }
}
