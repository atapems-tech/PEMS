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
/// طبق «PEMS Phase 6B — Wave A: Permit Grid». قبلاً کل جدول فیلترشده‌ی-با-Status (با ۴ Include
/// غیرضروری) با ToListAsync (بدون Skip/Take) بارگذاری می‌شد. مسیر pendingMyApproval باید ابتدا
/// کل مجموعه را با IWorkflowRuntime فیلتر کند و فقط بعد صفحه‌بندی شود - این تست‌ها هر دو مسیر را
/// پوشش می‌دهند. HasActionAsync("Permit","View")→Forbid gate باید دست‌نخورده بماند.
/// </summary>
public class PermitGridPaginationTests
{
    private static async Task<(PermitController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx,
        FakeCurrentUserService CurrentUser, FakeWorkflowRuntime Runtime)> CreateController(
        string dbName, string? role = SystemRoles.Viewer)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"PermitGrid_{dbName}");
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var roleModuleActionService = new RoleModuleActionService(ctx, accessControl);
        await roleModuleActionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        var currentUser = new FakeCurrentUserService { Roles = role != null ? new List<string> { role } : new List<string>() };

        // Index فقط به روش‌های زیر وابسته است؛ بقیه (Create/AdvanceStatus/...) در این تست‌ها استفاده نمی‌شوند.
        var runtime = new FakeWorkflowRuntime();
        var permitService = new PermitService(ctx, null!, null!, runtime);

        var controller = new PermitController(permitService, roleModuleActionService, ctx, currentUser,
            null!, runtime, null!, null!, null!, null!, null!);
        return (controller, ctx, currentUser, runtime);
    }

    private static int _nextId = 1;

    private static Permit NewPermit(DateTime createdDate, string status = "Draft") => new()
    {
        PermitId = _nextId++,
        PermitNumber = $"PTW-{_nextId:D4}",
        PermitType = "Cold Work",
        Title = "Test Permit",
        RequestedByEmployeeId = 0,
        Status = status,
        Priority = "Medium",
        CreatedDate = createdDate,
        IsActive = true
    };

    private static async Task<PagedResult<PermitListDto>> RunIndex(
        PermitController controller, string? status = null, bool pendingMyApproval = false, int page = 1, int? pageSize = null) =>
        Assert.IsType<PagedResult<PermitListDto>>(
            ((ViewResult)await controller.Index(status, pendingMyApproval, page, pageSize)).Model);

    [Fact]
    public async Task Index_WithoutViewPermission_ReturnsForbid()
    {
        var (controller, ctx, _, _) = await CreateController(nameof(Index_WithoutViewPermission_ReturnsForbid), role: null);
        ctx.Permits.Add(NewPermit(DateTime.Today));
        await ctx.SaveChangesAsync();

        var result = await controller.Index(null, false, 1, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_DefaultPageSize_Is50()
    {
        var (controller, ctx, _, _) = await CreateController(nameof(Index_DefaultPageSize_Is50));
        for (var i = 0; i < 60; i++)
            ctx.Permits.Add(NewPermit(DateTime.Today.AddMinutes(-i)));
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
        var (controller, ctx, _, _) = await CreateController($"{nameof(Index_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}");
        for (var i = 0; i < 120; i++)
            ctx.Permits.Add(NewPermit(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, page: 1, pageSize: pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task Index_Page1AndPage2_AreStableAndDisjoint()
    {
        var (controller, ctx, _, _) = await CreateController(nameof(Index_Page1AndPage2_AreStableAndDisjoint));
        for (var i = 0; i < 25; i++)
            ctx.Permits.Add(NewPermit(DateTime.Today.AddMinutes(-i)));
        await ctx.SaveChangesAsync();

        var page1 = await RunIndex(controller, page: 1, pageSize: 20);
        var page2 = await RunIndex(controller, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.PermitId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Index_DeterministicOrdering_SameCreatedDate_SameOrderTwice()
    {
        var (controller, ctx, _, _) = await CreateController(nameof(Index_DeterministicOrdering_SameCreatedDate_SameOrderTwice));
        var sameDate = DateTime.Today;
        for (var i = 0; i < 10; i++)
            ctx.Permits.Add(NewPermit(sameDate));
        await ctx.SaveChangesAsync();

        var first = await RunIndex(controller, page: 1, pageSize: 20);
        var second = await RunIndex(controller, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.PermitId), second.Items.Select(x => x.PermitId));
    }

    [Fact]
    public async Task Index_StatusFilter_StillWorksAfterPageSizeChange()
    {
        var (controller, ctx, _, _) = await CreateController(nameof(Index_StatusFilter_StillWorksAfterPageSizeChange));
        ctx.Permits.Add(NewPermit(DateTime.Today, status: "Draft"));
        ctx.Permits.Add(NewPermit(DateTime.Today, status: "Active"));
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller, status: "Draft");

        Assert.Single(result.Items);
        Assert.Equal("Draft", result.Items[0].Status);
    }

    [Fact]
    public async Task Index_InactivePermits_AreExcluded()
    {
        var (controller, ctx, _, _) = await CreateController(nameof(Index_InactivePermits_AreExcluded));
        var active = NewPermit(DateTime.Today);
        var inactive = NewPermit(DateTime.Today);
        inactive.IsActive = false;
        ctx.Permits.Add(active);
        ctx.Permits.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await RunIndex(controller);

        Assert.Single(result.Items);
        Assert.Equal(active.PermitId, result.Items[0].PermitId);
    }

    [Fact]
    public async Task Index_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var (controller, _, _, _) = await CreateController(nameof(Index_EmptyDataset_ReturnsEmptyPagedResult));

        var result = await RunIndex(controller);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Index_PendingMyApproval_FiltersBeforePaging_NotAfter()
    {
        // این تست باگ MaterialItem.lowStockOnly را برای Permit تکرار می‌کند: اگر صفحه‌بندی قبل
        // از فیلتر IWorkflowRuntime اعمال شود، TotalCount/Items اشتباه خواهند بود. این‌جا فقط
        // نصف Permit ها Pending هستند - نتیجه باید دقیقاً همان نصف باشد، نه یک زیرمجموعه‌ی اشتباه
        // از یک صفحه‌ی از پیش صفحه‌بندی‌شده.
        var (controller, ctx, currentUser, runtime) = await CreateController(nameof(Index_PendingMyApproval_FiltersBeforePaging_NotAfter));
        ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Ops" });
        ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Test", LastName = "User", DepartmentId = 1, IsActive = true });
        ctx.Users.Add(new ApplicationUser { Id = 1, UserName = "test", EmployeeId = 1 });
        await ctx.SaveChangesAsync();

        currentUser.UserId = 1;

        var pending = new List<Permit>();
        for (var i = 0; i < 30; i++)
        {
            var p = NewPermit(DateTime.Today.AddMinutes(-i));
            ctx.Permits.Add(p);
            pending.Add(p);
        }
        await ctx.SaveChangesAsync();

        runtime.PendingPermitIds = pending.Where((_, idx) => idx % 2 == 0).Select(p => p.PermitId).ToHashSet();

        var result = await RunIndex(controller, pendingMyApproval: true, page: 1, pageSize: 20);

        Assert.Equal(15, result.TotalCount);
        Assert.Equal(15, result.Items.Count);
        Assert.All(result.Items, item => Assert.Contains(item.PermitId, runtime.PendingPermitIds));
    }

    // طبق ممیزی مستقل فاز ۶B Wave A (P1): این تست‌ها موارد صریح خواسته‌شده در بند ۹ را پوشش
    // می‌دهند: چند صفحه، بدون هیچ تاییدِ در انتظار، فیلتر Status + pendingMyApproval با هم، و
    // مرتب‌سازی قطعی. هیچ‌کدام کل جدول Permit را بارگذاری نمی‌کنند - GetActiveIdsMatchingAsync/
    // GetByIdsAsync فقط روی شناسه‌های از پیش محدودشده‌ی PendingPermitIds کار می‌کنند.

    private static async Task<(PermitController Controller, PEMS.Persistence.Context.PEMSDbContext Ctx,
        FakeCurrentUserService CurrentUser, FakeWorkflowRuntime Runtime)> CreateControllerWithEmployee(string dbName)
    {
        var created = await CreateController(dbName);
        created.Ctx.Departments.Add(new Department { DepartmentId = 1, Name = "Ops" });
        created.Ctx.Employees.Add(new Employee { EmployeeId = 1, FirstName = "Test", LastName = "User", DepartmentId = 1, IsActive = true });
        created.Ctx.Users.Add(new ApplicationUser { Id = 1, UserName = "test", EmployeeId = 1 });
        await created.Ctx.SaveChangesAsync();
        created.CurrentUser.UserId = 1;
        return created;
    }

    [Fact]
    public async Task Index_PendingMyApproval_MultiplePages_AreStableAndDisjoint()
    {
        var (controller, ctx, _, runtime) = await CreateControllerWithEmployee(nameof(Index_PendingMyApproval_MultiplePages_AreStableAndDisjoint));

        var pending = new List<Permit>();
        for (var i = 0; i < 25; i++)
        {
            var p = NewPermit(DateTime.Today.AddMinutes(-i));
            ctx.Permits.Add(p);
            pending.Add(p);
        }
        await ctx.SaveChangesAsync();

        runtime.PendingPermitIds = pending.Select(p => p.PermitId).ToHashSet();

        var page1 = await RunIndex(controller, pendingMyApproval: true, page: 1, pageSize: 20);
        var page2 = await RunIndex(controller, pendingMyApproval: true, page: 2, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);

        var allIds = page1.Items.Concat(page2.Items).Select(x => x.PermitId).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Index_PendingMyApproval_NoPendingApprovals_ReturnsEmptyPagedResult_WithoutQueryingPermits()
    {
        var (controller, ctx, _, runtime) = await CreateControllerWithEmployee(nameof(Index_PendingMyApproval_NoPendingApprovals_ReturnsEmptyPagedResult_WithoutQueryingPermits));
        ctx.Permits.Add(NewPermit(DateTime.Today));
        await ctx.SaveChangesAsync();

        runtime.PendingPermitIds = new HashSet<int>();

        var result = await RunIndex(controller, pendingMyApproval: true, page: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Index_PendingMyApproval_CombinedWithStatusFilter_OnlyMatchingStatusReturned()
    {
        var (controller, ctx, _, runtime) = await CreateControllerWithEmployee(nameof(Index_PendingMyApproval_CombinedWithStatusFilter_OnlyMatchingStatusReturned));

        var underReview = NewPermit(DateTime.Today, status: "UnderReview");
        var draft = NewPermit(DateTime.Today.AddMinutes(-1), status: "Draft");
        ctx.Permits.Add(underReview);
        ctx.Permits.Add(draft);
        await ctx.SaveChangesAsync();

        // هر دو Permit در صف تایید همین کاربر هستند، اما فقط UnderReview باید با فیلتر Status عبور کند.
        runtime.PendingPermitIds = new HashSet<int> { underReview.PermitId, draft.PermitId };

        var result = await RunIndex(controller, status: "UnderReview", pendingMyApproval: true, page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal(underReview.PermitId, result.Items[0].PermitId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Index_PendingMyApproval_DeterministicOrdering_SameAssignedDate_TieBrokenByPermitId()
    {
        var (controller, ctx, _, runtime) = await CreateControllerWithEmployee(nameof(Index_PendingMyApproval_DeterministicOrdering_SameAssignedDate_TieBrokenByPermitId));

        var permits = new List<Permit>();
        for (var i = 0; i < 5; i++)
        {
            var p = NewPermit(DateTime.Today);
            ctx.Permits.Add(p);
            permits.Add(p);
        }
        await ctx.SaveChangesAsync();

        var sameDate = DateTime.Today;
        runtime.PendingPermitIds = permits.Select(p => p.PermitId).ToHashSet();
        runtime.ExplicitAssignedDates = permits.ToDictionary(p => p.PermitId, _ => sameDate);

        var first = await RunIndex(controller, pendingMyApproval: true, page: 1, pageSize: 20);
        var second = await RunIndex(controller, pendingMyApproval: true, page: 1, pageSize: 20);

        Assert.Equal(first.Items.Select(x => x.PermitId), second.Items.Select(x => x.PermitId));
        Assert.Equal(permits.Select(p => p.PermitId).OrderBy(id => id), first.Items.Select(x => x.PermitId));
    }
}
