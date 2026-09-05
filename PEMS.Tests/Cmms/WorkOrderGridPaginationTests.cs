using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B — Wave A: WorkOrder Grid». قبلاً کل جدول فیلترشده (با ۳ Include) با
/// ToListAsync (بدون Skip/Take) بارگذاری می‌شد؛ شمارنده‌های Dashboard هم با یک بارگذاری کامل
/// دوم محاسبه می‌شدند. Index فقط به Context/CurrentUser/PermissionService وابسته است - بقیه‌ی
/// وابستگی‌های Controller (Review/Labor/Lifecycle/Crew/Numbering/Stock) در این Action استفاده
/// نمی‌شوند. HasPermissionAsync→Forbid gate (WorkOrder.View) باید دست‌نخورده بماند.
/// </summary>
public class WorkOrderGridPaginationTests
{
    private static async Task<(WorkOrderController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateController(
        string dbName, string? role = SystemRoles.Viewer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"WorkOrderGrid_{dbName}");
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 1, Code = "MECH", Name = "Mechanical" });
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        await permissionService.SeedDefaultsAsync();

        // طبق الگوی AccessControlParityTests: HasPermissionAsync از AccessControlService می‌آید
        // که از Permissions/RolePermissionGrants می‌خواند - این‌ها با AccessControlSeeder از
        // RolePermissions همگام می‌شوند، نه مستقیم توسط PermissionService.SeedDefaultsAsync.
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };

        var controller = new WorkOrderController(ctx, null!, currentUser, permissionService, null!, null!, null!, null!, null!);
        return (controller, ctx);
    }

    private static int _nextId = 1;

    private static Equipment NewEquipment(string tag)
    {
        var id = _nextId++;
        return new Equipment { EquipmentId = id, TagNumber = tag, Name = tag, EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true };
    }

    private static WorkOrder NewWorkOrder(int equipmentId, DateTime createdDate, string status = "Open", string priority = "Medium") => new()
    {
        WorkOrderId = _nextId++,
        EquipmentId = equipmentId,
        WorkOrderNumber = $"WO-{Guid.NewGuid():N}".Substring(0, 12),
        WorkOrderType = "Corrective",
        Priority = priority,
        Status = status,
        CreatedDate = createdDate,
        IsActive = true
    };

    private static async Task<PagedResult<WorkOrderListDto>> RunIndex(
        WorkOrderController controller, string? status = null, int? equipmentId = null,
        bool assignedToMe = false, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<WorkOrderListDto>>(
            ((ViewResult)await controller.Index(status, equipmentId, assignedToMe, page, pageSize)).Model);

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, ctx) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);
        var eq = NewEquipment("TAG-001");
        ctx.Equipments.Add(eq);
        ctx.WorkOrders.Add(NewWorkOrder(eq.EquipmentId, DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await controller.Index(null, null, false, 1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var (controller, ctx) = await CreateController(nameof(Index_DefaultPageSize_Is50));
        for (var i = 0; i < 60; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.WorkOrders.Add(NewWorkOrder(eq.EquipmentId, DateTime.Today.AddMinutes(-i)));
        }
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Index_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var (controller, ctx) = await CreateController($"{nameof(Index_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}");
        for (var i = 0; i < 120; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.WorkOrders.Add(NewWorkOrder(eq.EquipmentId, DateTime.Today.AddMinutes(-i)));
        }
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task Index_Page1AndPage2_AreStableAndDisjoint()
    {
        var (controller, ctx) = await CreateController(nameof(Index_Page1AndPage2_AreStableAndDisjoint));
        for (var i = 0; i < 25; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.WorkOrders.Add(NewWorkOrder(eq.EquipmentId, DateTime.Today.AddMinutes(-i)));
        }
        await ctx.SaveChangesAsync();

        var page1 = await RunIndex(controller, page: 1, pageSize: 20);
        var page2 = await RunIndex(controller, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.WorkOrderId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Index_DeterministicOrdering_SameCreatedDate_SameOrderTwice()
    {
        var (controller, ctx) = await CreateController(nameof(Index_DeterministicOrdering_SameCreatedDate_SameOrderTwice));
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
        {
            var eq = NewEquipment($"TAG-{i:D3}");
            ctx.Equipments.Add(eq);
            ctx.WorkOrders.Add(NewWorkOrder(eq.EquipmentId, sameDate));
        }
        await ctx.SaveChangesAsync();

        var first = await RunIndex(controller, page: 1, pageSize: 20);
        var second = await RunIndex(controller, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.WorkOrderId), second.Items.Select(x => x.WorkOrderId));
    }

    [Fact]
    public async Task Index_StatusFilter_StillWorksAfterPageSizeChange()
    {
        var (controller, ctx) = await CreateController(nameof(Index_StatusFilter_StillWorksAfterPageSizeChange));
        var eq1 = NewEquipment("TAG-001");
        var eq2 = NewEquipment("TAG-002");
        ctx.Equipments.AddRange(eq1, eq2);
        ctx.WorkOrders.Add(NewWorkOrder(eq1.EquipmentId, DateTime.Today, status: "Open"));
        ctx.WorkOrders.Add(NewWorkOrder(eq2.EquipmentId, DateTime.Today, status: "Closed"));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, status: "Open", page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal("Open", result.Items[0].Status);
    }

    [Fact]
    public async Task Index_DashboardCounts_AreGlobal_NotRestrictedByStatusFilter()
    {
        // شمارنده‌های بالای صفحه باید کل جدول را نشان دهند - حتی وقتی کاربر روی یک Status
        // خاص فیلتر کرده. این رفتار قبلی (allActive بدون فیلتر) باید حفظ شود.
        var (controller, ctx) = await CreateController(nameof(Index_DashboardCounts_AreGlobal_NotRestrictedByStatusFilter));
        var eq1 = NewEquipment("TAG-001");
        var eq2 = NewEquipment("TAG-002");
        var eq3 = NewEquipment("TAG-003");
        ctx.Equipments.AddRange(eq1, eq2, eq3);
        ctx.WorkOrders.Add(NewWorkOrder(eq1.EquipmentId, DateTime.Today, status: "Open"));
        ctx.WorkOrders.Add(NewWorkOrder(eq2.EquipmentId, DateTime.Today, status: "Open"));
        ctx.WorkOrders.Add(NewWorkOrder(eq3.EquipmentId, DateTime.Today, status: "Closed", priority: "Critical"));
        await ctx.SaveChangesAsync();

        var view = (ViewResult)await controller.Index(status: "Open", equipmentId: null, assignedToMe: false, page: 1, pageSize: 20);

        Assert.Equal(2, (int)view.ViewData["OpenCount"]!);
        Assert.Equal(1, (int)view.ViewData["CompletedCount"]!); // Closed را هم می‌شمارد - مستقل از فیلتر Open فعلی
    }

    [Fact]
    public async Task Index_InactiveWorkOrders_AreExcluded()
    {
        var (controller, ctx) = await CreateController(nameof(Index_InactiveWorkOrders_AreExcluded));
        var eq1 = NewEquipment("TAG-ACTIVE");
        var eq2 = NewEquipment("TAG-INACTIVE");
        ctx.Equipments.AddRange(eq1, eq2);
        var active = NewWorkOrder(eq1.EquipmentId, DateTime.Today);
        var inactive = NewWorkOrder(eq2.EquipmentId, DateTime.Today);
        inactive.IsActive = false;
        ctx.WorkOrders.Add(active);
        ctx.WorkOrders.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller);

        Assert.Single(result.Items);
        Assert.Equal(active.WorkOrderId, result.Items[0].WorkOrderId);
    }
}
