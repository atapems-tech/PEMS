using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 6B Wave B1 — ممیزی مستقل نهایی» بند P0: JobPlanController هیچ بررسی
/// مجوزی نداشت - هر کاربر احرازهویت‌شده‌ای (بدون توجه به نقش) می‌توانست Job Plan بسازد/
/// ویرایش کند.
/// </summary>
public class JobPlanControllerAuthorizationTests
{
    // NumberingService واقعی از UPDATE...OUTPUT خام SQL Server استفاده می‌کند (با InMemory کار
    // نمی‌کند) - این Fake ساده فقط برای اثبات مسیر مجوز لازم است، نه خودِ منطق شماره‌گذاری.
    private class FakeNumberingService : INumberingService
    {
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-0001");
    }

    private static async Task<(JobPlanController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.MaintenanceEngineer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"JobPlanAuth_{dbName}");
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

        var jobPlanService = new JobPlanService(ctx, new FakeNumberingService());
        var controller = new JobPlanController(jobPlanService, ctx, currentUser, permissionService).WithTempData();
        return (controller, ctx);
    }

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);

        var result = await controller.Index();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_WithViewPermission_ReturnsView()
    {
        var (controller, _) = await CreateController(nameof(Index_WithViewPermission_ReturnsView), role: SystemRoles.Viewer);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Create_WithoutCreatePermission_ReturnsForbid), role: SystemRoles.Viewer);

        var result = await controller.Create("Test Plan", null, null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_WithCreatePermission_CreatesJobPlan()
    {
        var (controller, ctx) = await CreateController(nameof(Create_WithCreatePermission_CreatesJobPlan));

        var result = await controller.Create("Test Plan", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.JobPlans);
    }

    [Fact]
    public async Task Details_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Details_WithoutViewPermission_ReturnsForbid), role: null);
        var jp = new PEMS.Domain.Entities.JobPlan { JobPlanId = 1, JobPlanNumber = "JP-001", Title = "T", IsActive = true };
        ctx.JobPlans.Add(jp);
        await ctx.SaveChangesAsync();

        var result = await controller.Details(jp.JobPlanId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Edit_WithoutEditPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Edit_WithoutEditPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var jp = new PEMS.Domain.Entities.JobPlan { JobPlanId = 1, JobPlanNumber = "JP-001", Title = "Original", IsActive = true };
        ctx.JobPlans.Add(jp);
        await ctx.SaveChangesAsync();

        var result = await controller.Edit(jp.JobPlanId, "Renamed", null, null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Original", ctx.JobPlans.Single().Title);
    }

    [Fact]
    public async Task Edit_WithEditPermission_UpdatesJobPlan()
    {
        var (controller, ctx) = await CreateController(nameof(Edit_WithEditPermission_UpdatesJobPlan));
        var jp = new PEMS.Domain.Entities.JobPlan { JobPlanId = 1, JobPlanNumber = "JP-001", Title = "Original", IsActive = true };
        ctx.JobPlans.Add(jp);
        await ctx.SaveChangesAsync();

        var result = await controller.Edit(jp.JobPlanId, "Renamed", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Renamed", ctx.JobPlans.Single().Title);
    }

    [Fact]
    public async Task AddTask_WithoutEditPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(AddTask_WithoutEditPermission_ReturnsForbid), role: SystemRoles.Viewer);
        var jp = new PEMS.Domain.Entities.JobPlan { JobPlanId = 1, JobPlanNumber = "JP-001", Title = "T", IsActive = true };
        ctx.JobPlans.Add(jp);
        await ctx.SaveChangesAsync();

        var result = await controller.AddTask(jp.JobPlanId, 1, "New Task", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaintenanceTasks);
    }
}
