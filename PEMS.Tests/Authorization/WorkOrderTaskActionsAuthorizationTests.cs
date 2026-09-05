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
/// طبق «PEMS Phase 6B Wave A — ممیزی مستقل» بند P0: WorkOrderController.CompleteTask/
/// ReopenTask هیچ بررسی مجوزی نداشتند - برخلاف بقیه‌ی جهش‌های همین کنترلر (Hold/Resume/
/// AddLaborEntry/...) که همه به "WorkOrder"/"Edit" گیت شده‌اند.
/// </summary>
public class WorkOrderTaskActionsAuthorizationTests
{
    private static async Task<(WorkOrderController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.MaintenanceEngineer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WOTaskAuth_{dbName}");
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

        var controller = new WorkOrderController(ctx, null!, currentUser, permissionService, null!, null!, null!, null!, null!);
        return (controller, ctx);
    }

    [Fact]
    public async Task CompleteTask_WithoutEditPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(CompleteTask_WithoutEditPermission_ReturnsForbid), role: null);
        var task = new WorkOrderTask { WorkOrderTaskId = 1, WorkOrderId = 1, TaskNumber = 1, Title = "T", IsCompleted = false };
        ctx.WorkOrderTasks.Add(task);
        await ctx.SaveChangesAsync();

        var result = await controller.CompleteTask(task.WorkOrderTaskId, task.WorkOrderId);

        Assert.IsType<ForbidResult>(result);
        Assert.False(ctx.WorkOrderTasks.Single().IsCompleted);
    }

    [Fact]
    public async Task CompleteTask_WithEditPermission_CompletesTask()
    {
        var (controller, ctx) = await CreateController(nameof(CompleteTask_WithEditPermission_CompletesTask));
        var task = new WorkOrderTask { WorkOrderTaskId = 1, WorkOrderId = 1, TaskNumber = 1, Title = "T", IsCompleted = false };
        ctx.WorkOrderTasks.Add(task);
        await ctx.SaveChangesAsync();

        var result = await controller.CompleteTask(task.WorkOrderTaskId, task.WorkOrderId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(ctx.WorkOrderTasks.Single().IsCompleted);
    }

    [Fact]
    public async Task ReopenTask_WithoutEditPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(ReopenTask_WithoutEditPermission_ReturnsForbid), role: null);
        var task = new WorkOrderTask { WorkOrderTaskId = 1, WorkOrderId = 1, TaskNumber = 1, Title = "T", IsCompleted = true, CompletedDate = DateTime.Now };
        ctx.WorkOrderTasks.Add(task);
        await ctx.SaveChangesAsync();

        var result = await controller.ReopenTask(task.WorkOrderTaskId, task.WorkOrderId);

        Assert.IsType<ForbidResult>(result);
        Assert.True(ctx.WorkOrderTasks.Single().IsCompleted);
    }

    [Fact]
    public async Task ReopenTask_WithEditPermission_ReopensTask()
    {
        var (controller, ctx) = await CreateController(nameof(ReopenTask_WithEditPermission_ReopensTask));
        var task = new WorkOrderTask { WorkOrderTaskId = 1, WorkOrderId = 1, TaskNumber = 1, Title = "T", IsCompleted = true, CompletedDate = DateTime.Now };
        ctx.WorkOrderTasks.Add(task);
        await ctx.SaveChangesAsync();

        var result = await controller.ReopenTask(task.WorkOrderTaskId, task.WorkOrderId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(ctx.WorkOrderTasks.Single().IsCompleted);
    }
}
