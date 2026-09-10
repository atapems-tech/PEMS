using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 7.5 — CMMS Operational Data Readiness & Master Data Activation» ممیزی:
/// MaintenanceStrategyController پیش از این فاز هیچ بررسی مجوزی نداشت - هر کاربر
/// احرازهویت‌شده‌ای (بدون توجه به نقش) می‌توانست برای هر تجهیزی، حتی به‌صورت Bulk برای صدها
/// تجهیز هم‌زمان، استراتژی نگهداری بسازد یا غیرفعال کند. این تست‌ها اثبات می‌کنند گیت جدید
/// واقعاً کار می‌کند و BulkCreate (بالاترین ریسک، طبق بند ۴۳) فقط برای Supervisor/Manager
/// (Approve) در دسترس است، نه MaintenanceEngineer.
/// </summary>
public class MaintenanceStrategyControllerAuthorizationTests
{
    private static async Task<(MaintenanceStrategyController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.MaintenanceEngineer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"MaintStrategyAuth_{dbName}");
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

        var strategyService = new MaintenanceStrategyService(ctx);
        var controller = new MaintenanceStrategyController(ctx, strategyService, currentUser, permissionService).WithTempData();
        return (controller, ctx);
    }

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, _) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);

        var result = await controller.Index(null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ViewerRole_CanView()
    {
        var (controller, _) = await CreateController(nameof(Index_ViewerRole_CanView), role: SystemRoles.Viewer);

        var result = await controller.Index(null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Create_ViewerRole_ReturnsForbid_NoStrategyCreated()
    {
        var (controller, ctx) = await CreateController(nameof(Create_ViewerRole_ReturnsForbid_NoStrategyCreated), role: SystemRoles.Viewer);
        var equipment = new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var result = await controller.Create(equipment.EquipmentId, "Preventive", "LEAK", "Operational", "test", "Calendar", null, null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task Create_MaintenanceEngineer_CanCreate()
    {
        var (controller, ctx) = await CreateController(nameof(Create_MaintenanceEngineer_CanCreate), role: SystemRoles.MaintenanceEngineer);
        var equipment = new Equipment { TagNumber = "EQ-2", Name = "EQ-2", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var result = await controller.Create(equipment.EquipmentId, "Preventive", "LEAK", "Operational", "test", "Calendar", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task Create_DuplicateEquipmentAndFailureMode_RejectedWithoutCreatingSecondRow()
    {
        // طبق بند ۳۴: همان کلید Equipment+FailureModeCode که RcmOperationalBridgeService (فاز ۶.۱) استفاده می‌کند
        var (controller, ctx) = await CreateController(nameof(Create_DuplicateEquipmentAndFailureMode_RejectedWithoutCreatingSecondRow));
        var equipment = new Equipment { TagNumber = "EQ-3", Name = "EQ-3", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var first = await controller.Create(equipment.EquipmentId, "Preventive", "LEAK", "Operational", "first", "Calendar", null, null, null);
        var second = await controller.Create(equipment.EquipmentId, "Predictive", "LEAK", "Operational", "second attempt", "Calendar", null, null, null);

        Assert.IsType<RedirectToActionResult>(first);
        Assert.IsType<RedirectToActionResult>(second); // نه خطای سیستمی - فقط رد کسب‌وکاری با پیام
        Assert.Single(ctx.MaintenanceStrategies); // هرگز رکورد دوم ساخته نشد
    }

    [Fact]
    public async Task BulkCreate_Get_MaintenanceEngineer_ReturnsForbid()
    {
        // طبق بند ۴۳: فعال‌سازی هم‌زمان چند تجهیز، بالاترین ریسک است - فقط Approve (Supervisor/Manager)
        var (controller, _) = await CreateController(nameof(BulkCreate_Get_MaintenanceEngineer_ReturnsForbid), role: SystemRoles.MaintenanceEngineer);

        var result = await controller.BulkCreate(null, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BulkCreate_Get_EngineeringSupervisor_CanView()
    {
        var (controller, _) = await CreateController(nameof(BulkCreate_Get_EngineeringSupervisor_CanView), role: SystemRoles.EngineeringSupervisor);

        var result = await controller.BulkCreate(null, null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task BulkCreate_Post_MaintenanceEngineer_ReturnsForbid_NoStrategiesCreated()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Post_MaintenanceEngineer_ReturnsForbid_NoStrategiesCreated), role: SystemRoles.MaintenanceEngineer);
        var e1 = new Equipment { TagNumber = "EQ-4", Name = "EQ-4", EquipmentType = "Pump", IsActive = true };
        var e2 = new Equipment { TagNumber = "EQ-5", Name = "EQ-5", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.AddRange(e1, e2);
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(new List<int> { e1.EquipmentId, e2.EquipmentId },
            "Preventive", "LEAK", "Operational", "bulk", "Calendar", null, null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task BulkCreate_Post_EngineeringSupervisor_CreatesForAllSelectedEquipment()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Post_EngineeringSupervisor_CreatesForAllSelectedEquipment), role: SystemRoles.EngineeringSupervisor);
        var e1 = new Equipment { TagNumber = "EQ-6", Name = "EQ-6", EquipmentType = "Pump", IsActive = true };
        var e2 = new Equipment { TagNumber = "EQ-7", Name = "EQ-7", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.AddRange(e1, e2);
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(new List<int> { e1.EquipmentId, e2.EquipmentId },
            "Preventive", "LEAK", "Operational", "bulk", "Calendar", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(2, await ctx.MaintenanceStrategies.CountAsync());
    }

    [Fact]
    public async Task BulkCreate_Post_SkipsEquipmentThatAlreadyHasMatchingActiveStrategy()
    {
        var (controller, ctx) = await CreateController(nameof(BulkCreate_Post_SkipsEquipmentThatAlreadyHasMatchingActiveStrategy), role: SystemRoles.EngineeringSupervisor);
        var e1 = new Equipment { TagNumber = "EQ-8", Name = "EQ-8", EquipmentType = "Pump", IsActive = true };
        var e2 = new Equipment { TagNumber = "EQ-9", Name = "EQ-9", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.AddRange(e1, e2);
        await ctx.SaveChangesAsync();
        ctx.MaintenanceStrategies.Add(new MaintenanceStrategy { EquipmentId = e1.EquipmentId, StrategyType = "Preventive", FailureModeCode = "LEAK", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await controller.BulkCreate(new List<int> { e1.EquipmentId, e2.EquipmentId },
            "Preventive", "LEAK", "Operational", "bulk", "Calendar", null, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(2, await ctx.MaintenanceStrategies.CountAsync()); // فقط e2 اضافه شد؛ e1 تکراری نشد
    }

    [Fact]
    public async Task Deactivate_ViewerRole_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Deactivate_ViewerRole_ReturnsForbid), role: SystemRoles.Viewer);
        var equipment = new Equipment { TagNumber = "EQ-10", Name = "EQ-10", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "Preventive", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();

        var result = await controller.Deactivate(strategy.MaintenanceStrategyId);

        Assert.IsType<ForbidResult>(result);
        Assert.True(ctx.MaintenanceStrategies.Single().IsActive);
    }
}
