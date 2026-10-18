using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.Replenishment;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// Phase 2 / P5 follow-up - تخصیص مجوز تأییدشده توسط مالک برای ماژول ReplenishmentRecommendation.
/// اعمال‌شده: «Warehouse Manager» = نقش WH.Manager ⇒ View + Create + Edit (فقط). سه نقش دیگرِ تأییدشده (Warehouse Clerk / Procurement / Maintenance Planner)
/// شناسه‌ی بی‌ابهام ندارند (نگاه کنید به docs/procurement/P5_REPLENISHMENT_RECOMMENDATIONS.md) پس عمداً هیچ مجوزی نمی‌گیرند.
/// </summary>
public class ReplenishmentPermissionAssignmentTests
{
    private const string Module = IReplenishmentRecommendationService.Module;
    private static readonly string[] Actions = { "View", "Create", "Edit", "Approve", "Delete" };

    private static async Task SeedAllAsync(PEMSDbContext ctx, bool includeCatalogRoles = true)
    {
        var access = new AccessControlService(ctx);
        var permissions = new PermissionService(ctx, access);
        await permissions.SeedDefaultsAsync();                              // همان ترتیب Program.cs
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        if (includeCatalogRoles) await RoleCatalogSeeder.SeedAsync(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());
    }

    private static async Task<PEMSDbContext> NewSeededAsync(string name)
    {
        var ctx = PemsDbContextFactory.CreateInMemory("ReplPerm_" + name + Guid.NewGuid().ToString("N"));
        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();
        await SeedAllAsync(ctx);
        return ctx;
    }

    private static string Row(RolePermission r) =>
        $"{r.RoleName}|{r.ModuleName}|{r.CanView}|{r.CanCreate}|{r.CanEdit}|{r.CanDelete}|{r.CanApprove}|{r.CanManualNumber}|{r.CanFreeze}|{r.CanUnfreeze}|{r.CanHold}|{r.CanReleaseHold}";

    [Fact]
    public async Task WarehouseManager_GetsViewCreateEdit_AndNothingElse_ThroughTheRealEngine()
    {
        await using var ctx = await NewSeededAsync(nameof(WarehouseManager_GetsViewCreateEdit_AndNothingElse_ThroughTheRealEngine));
        var perms = new PermissionService(ctx, new AccessControlService(ctx));
        var role = new[] { RoleCatalog.WarehouseManager };

        Assert.True(await perms.HasPermissionAsync(role, Module, "View"));
        Assert.True(await perms.HasPermissionAsync(role, Module, "Create"));
        Assert.True(await perms.HasPermissionAsync(role, Module, "Edit"));
        Assert.False(await perms.HasPermissionAsync(role, Module, "Approve"));       // تبدیل/تأیید خرید (P6) وجود ندارد
        Assert.False(await perms.HasPermissionAsync(role, Module, "Delete"));
    }

    [Fact]
    public async Task PermissionMatrix_ShowsExactlyOneRow_ForTheModule()
    {
        await using var ctx = await NewSeededAsync(nameof(PermissionMatrix_ShowsExactlyOneRow_ForTheModule));
        var matrix = await new PermissionService(ctx, new AccessControlService(ctx)).GetMatrixAsync();

        var rows = matrix.Where(m => m.ModuleName == Module).ToList();

        var row = Assert.Single(rows);
        Assert.Equal(RoleCatalog.WarehouseManager, row.RoleName);
        Assert.Equal((true, true, true, false, false), (row.CanView, row.CanCreate, row.CanEdit, row.CanDelete, row.CanApprove));
        Assert.False(row.CanManualNumber || row.CanFreeze || row.CanUnfreeze || row.CanHold || row.CanReleaseHold);
    }

    [Fact]
    public async Task EveryOtherRole_HasNoPermissionOnTheModule_IncludingTheOwnerApprovedButUnresolvedOnes()
    {
        await using var ctx = await NewSeededAsync(nameof(EveryOtherRole_HasNoPermissionOnTheModule_IncludingTheOwnerApprovedButUnresolvedOnes));
        var perms = new PermissionService(ctx, new AccessControlService(ctx));

        var others = SystemRoles.All.Concat(RoleCatalog.Definitions.Select(d => d.Name))
            .Distinct().Where(r => r != RoleCatalog.WarehouseManager && r != SystemRoles.SystemAdmin).ToList();

        Assert.Contains(RoleCatalog.WarehouseStorekeeper, others);      // کاندید «Warehouse Clerk» - هنوز تأیید نشده
        Assert.Contains("MNT.PlanningExpert", others);                    // کاندید «Maintenance Planner» - هنوز تأیید نشده
        foreach (var role in others)
            foreach (var action in Actions)
                Assert.False(await perms.HasPermissionAsync(new[] { role }, Module, action), $"{role} must not have {Module}.{action}");
    }

    [Fact]
    public async Task SystemAdmin_KeepsOnlyTheGenericBypass_NoExplicitRowOrGrantIsCreated()
    {
        await using var ctx = await NewSeededAsync(nameof(SystemAdmin_KeepsOnlyTheGenericBypass_NoExplicitRowOrGrantIsCreated));
        var perms = new PermissionService(ctx, new AccessControlService(ctx));

        Assert.True(await perms.HasPermissionAsync(new[] { SystemRoles.SystemAdmin }, Module, "View"));    // Bypass عمومی موجود
        Assert.DoesNotContain(await ctx.RolePermissions.Where(p => p.ModuleName == Module).ToListAsync(), p => p.RoleName == SystemRoles.SystemAdmin);
        Assert.False(AccessControlService.RequiresExplicitGrant(Module, "Create"));
        Assert.False(AccessControlService.RequiresExplicitGrant(Module, "Edit"));
    }

    [Fact]
    public async Task UpgradeOfAnExistingDatabase_AddsOnlyTheNewModuleRow_AndLeavesEveryOtherPermissionUntouched()
    {
        await using var ctx = await NewSeededAsync(nameof(UpgradeOfAnExistingDatabase_AddsOnlyTheNewModuleRow_AndLeavesEveryOtherPermissionUntouched));

        // شبیه‌سازی دیتابیس پیش از P5: ردیف‌ها و Grantهای این ماژول حذف می‌شوند
        var modulePermIds = await ctx.Permissions.Where(p => p.ResourceName == Module).Select(p => p.PermissionId).ToListAsync();
        ctx.RolePermissionGrants.RemoveRange(ctx.RolePermissionGrants.Where(g => modulePermIds.Contains(g.PermissionId)));
        ctx.Permissions.RemoveRange(ctx.Permissions.Where(p => p.ResourceName == Module));
        ctx.RolePermissions.RemoveRange(ctx.RolePermissions.Where(p => p.ModuleName == Module));
        await ctx.SaveChangesAsync();
        // یک تنظیم دستی مدیر روی ماژولی دیگر: باید دقیقاً حفظ شود
        var manual = await ctx.RolePermissions.FirstAsync(p => p.ModuleName == "StoreRequisition" && p.RoleName == RoleCatalog.WarehouseManager);
        manual.CanDelete = true;
        await ctx.SaveChangesAsync();
        var services0 = new ServiceCollection();
        services0.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services0.BuildServiceProvider());      // همگام‌سازی همان Startup قبلی

        async Task<(List<string> Rows, List<string> Grants)> SnapshotAsync() => (
            (await ctx.RolePermissions.AsNoTracking().Where(p => p.ModuleName != Module).ToListAsync()).Select(Row).OrderBy(x => x).ToList(),
            (await ctx.RolePermissionGrants.AsNoTracking().Include(g => g.Permission).Where(g => g.Permission!.ResourceName != Module).ToListAsync())
                .Select(g => $"{g.RoleId}|{g.Permission!.ResourceName}|{g.Permission.ActionName}|{g.IsActive}").OrderBy(x => x).ToList());
        var before = await SnapshotAsync();

        await SeedAllAsync(ctx);                                         // «اجرای Startup نسخه‌ی جدید روی دیتابیس موجود»

        var after = await SnapshotAsync();
        Assert.Equal(before.Rows, after.Rows);                           // هیچ مجوز ماژول دیگری تغییر/اضافه/حذف نشد (از جمله تنظیم دستی)
        Assert.Equal(before.Grants, after.Grants);
        var added = await ctx.RolePermissions.AsNoTracking().Where(p => p.ModuleName == Module).ToListAsync();
        Assert.Equal(RoleCatalog.WarehouseManager, Assert.Single(added).RoleName);
    }

    [Fact]
    public async Task AdminEditOfTheAssignment_IsNeverOverwrittenByTheSeed_AndSeedingIsIdempotent()
    {
        await using var ctx = await NewSeededAsync(nameof(AdminEditOfTheAssignment_IsNeverOverwrittenByTheSeed_AndSeedingIsIdempotent));
        var access = new AccessControlService(ctx);
        var perms = new PermissionService(ctx, access);

        // مدیر Edit را از مدیر انبار برمی‌دارد (از همان مسیر ماتریس مجوز)
        await perms.SetPermissionsAsync(RoleCatalog.WarehouseManager, new[] { new ModulePermissionInput { ModuleName = Module, CanView = true, CanCreate = true, CanEdit = false } });
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());
        Assert.False(await perms.HasPermissionAsync(new[] { RoleCatalog.WarehouseManager }, Module, "Edit"));

        await SeedAllAsync(ctx);
        await SeedAllAsync(ctx);

        Assert.False(await perms.HasPermissionAsync(new[] { RoleCatalog.WarehouseManager }, Module, "Edit"));       // بازنویسی نشد
        Assert.Single(await ctx.RolePermissions.Where(p => p.ModuleName == Module).ToListAsync());                  // تکراری نشد
    }

    [Fact]
    public async Task TheAssignmentAddsNoPurchasingApprovalReservationOrWorkflowPermission()
    {
        await using var ctx = await NewSeededAsync(nameof(TheAssignmentAddsNoPurchasingApprovalReservationOrWorkflowPermission));
        var perms = new PermissionService(ctx, new AccessControlService(ctx));
        var role = new[] { RoleCatalog.WarehouseManager };

        // قبل از این تخصیص هم، مدیر انبار این مجوزهای خرید/تأیید را نداشت؛ تخصیص جدید چیزی به آن‌ها اضافه نمی‌کند
        foreach (var module in new[] { "PurchaseOrder", "ProcurementPackage", "MaterialRequisition", "Tender", "Contracts", "WorkOrder" })
            foreach (var action in new[] { "Create", "Approve", "Issue" })
                Assert.False(await perms.HasPermissionAsync(role, module, action), $"WH.Manager must not have {module}.{action}");
    }
}
