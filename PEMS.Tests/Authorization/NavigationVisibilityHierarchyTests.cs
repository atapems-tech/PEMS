using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Phase 6 — Hierarchical Role/Discipline Visibility Correction»: قانون مطلق سند -
/// Navigation Node ≠ Permission ≠ Role ≠ Discipline. این تست‌ها ثابت می‌کنند
/// NavigationVisibilityTreeBuilder/NavigationCatalog این قانون را نقض نمی‌کنند - Permission هرگز
/// همشأن یک آیتم ناوبری نمی‌شود، سلسله‌مراتب واقعی سایدبار دست‌نخورده می‌ماند، نقش/دیسیپلین همیشه
/// Context زیرِ یک گره‌ی واقعی است، و تغییر Permission Matrix هرگز گره‌ی ناوبری تازه نمی‌سازد.
/// </summary>
public class NavigationVisibilityHierarchyTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, NavigationVisibilityResolver resolver, PermissionService permissionService)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var access = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, access);
        await permissionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();
        await AccessControlSeeder.SeedFromLegacyAsync(provider);
        await AccessControlSeeder.SeedPhase3PermissionsAsync(provider);

        var overrides = new NavigationVisibilityOverrideService(ctx);
        var resolver = new NavigationVisibilityResolver(overrides, access);
        return (ctx, resolver, permissionService);
    }

    private static async Task<List<NavigationModuleNode>> BuildTreeAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, NavigationVisibilityResolver resolver, params string[] roleNames)
    {
        var roleTuples = await ctx.Roles
            .Where(r => r.Name != null && roleNames.Contains(r.Name))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();
        var tuples = roleTuples.Select(r => (r.Id, r.Name!)).ToList();

        var rowsByRole = new Dictionary<string, List<NavigationVisibilityRowDescription>>();
        foreach (var role in tuples)
            rowsByRole[role.Item2] = await resolver.DescribeForRoleAsync(role.Item2);

        return NavigationVisibilityTreeBuilder.BuildTree(tuples, rowsByRole);
    }

    // ---------------------------------------------------------------------
    // Test 1 — Permission is not navigation
    // ---------------------------------------------------------------------
    [Fact]
    public void Test1_No_navigation_key_looks_like_a_permission_name()
    {
        // نام واقعی Permission همیشه به شکل "Resource.Action" است (نقطه‌دار - طبق Permission.Name
        // در سراسر پروژه). هیچ ModuleKey/ItemKey نباید این قالب را داشته باشد - چون یعنی یک
        // Permission به‌جای یک گره‌ی ناوبری نشسته.
        foreach (var moduleKey in NavigationCatalog.ModuleKeys)
            Assert.DoesNotContain(".", moduleKey);

        foreach (var itemKey in NavigationCatalog.CmmsIntegrationItemResourceMap.Keys)
            Assert.DoesNotContain(".", itemKey);
    }

    [Fact]
    public async Task Test1_Capability_metadata_never_becomes_a_sibling_navigation_item()
    {
        var (ctx, resolver, _) = await SeedAsync(nameof(Test1_Capability_metadata_never_becomes_a_sibling_navigation_item));
        var tree = await BuildTreeAsync(ctx, resolver, SystemRoles.MaintenanceEngineer);

        var cmms = tree.Single(m => m.ModuleKey == "CmmsIntegration");
        var workOrder = cmms.Items.Single(i => i.ItemKey == "WorkOrder");

        // "Capability" فقط یک خاصیت رشته‌ای روی همان گره است - نه یک آیتم مستقل با ItemKey خودش
        Assert.Equal("WorkOrder.View", workOrder.Capability);
        Assert.DoesNotContain(cmms.Items, i => i.ItemKey == "WorkOrder.View");
        Assert.DoesNotContain(cmms.Items, i => i.ItemKey!.Contains("."));
    }

    // ---------------------------------------------------------------------
    // Test 2 — Navigation hierarchy is preserved
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Test2_Real_sidebar_hierarchy_is_preserved_module_subgroup_item()
    {
        var (ctx, resolver, _) = await SeedAsync(nameof(Test2_Real_sidebar_hierarchy_is_preserved_module_subgroup_item));
        var tree = await BuildTreeAsync(ctx, resolver, SystemRoles.MaintenanceEngineer);

        var cmms = tree.Single(m => m.ModuleKey == "CmmsIntegration");

        // WorkOrder/WorkRequest/Equipment مستقیماً زیر CmmsIntegration‌اند - نه زیر هیچ زیرگروهی
        Assert.Contains(cmms.Items, i => i.ItemKey == "WorkOrder");
        Assert.Contains(cmms.Items, i => i.ItemKey == "WorkRequest");
        Assert.Contains(cmms.Items, i => i.ItemKey == "Equipment");

        // «Maintenance Planning» دقیقاً همان چهار آیتمی که در _SidebarMenu.cshtml زیر همین تیتر است
        var planning = cmms.Subgroups.Single(s => s.Label == "Maintenance Planning");
        Assert.Equal(
            new[] { "MaintenanceStrategy", "MaintenancePlan", "CmmsOperations", "MaintenanceCalendar" },
            planning.Items.Select(i => i.ItemKey).ToArray());

        // آیتم‌های زیرگروه دیگر مستقیماً زیر ماژول تکرار نشده‌اند
        Assert.DoesNotContain(cmms.Items, i => i.ItemKey == "MaintenanceStrategy");
    }

    // ---------------------------------------------------------------------
    // Test 3 — Role is contextual (never a sibling/top-level node)
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Test3_Role_access_is_nested_under_its_navigation_item_never_a_node_of_its_own()
    {
        var (ctx, resolver, _) = await SeedAsync(nameof(Test3_Role_access_is_nested_under_its_navigation_item_never_a_node_of_its_own));
        var tree = await BuildTreeAsync(ctx, resolver, SystemRoles.MaintenanceEngineer, SystemRoles.EngineeringManager);

        // هیچ ModuleKey ای نام یک نقش واقعی نیست - نقش هرگز ماژول/آیتم نمی‌شود
        Assert.DoesNotContain(tree, m => m.ModuleKey == SystemRoles.MaintenanceEngineer || m.ModuleKey == SystemRoles.EngineeringManager);

        var cmms = tree.Single(m => m.ModuleKey == "CmmsIntegration");
        var workOrder = cmms.Items.Single(i => i.ItemKey == "WorkOrder");

        // دسترسی هر دو نقش دقیقاً زیر همین یک گره‌ی WorkOrder است - نه دو گره‌ی جدا
        Assert.Equal(2, workOrder.RoleAccess.Count);
        Assert.Contains(workOrder.RoleAccess, r => r.RoleName == SystemRoles.MaintenanceEngineer);
        Assert.Contains(workOrder.RoleAccess, r => r.RoleName == SystemRoles.EngineeringManager);
    }

    // ---------------------------------------------------------------------
    // Test 4 — No duplicate nodes
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Test4_Item_count_matches_the_catalog_exactly_no_permission_generated_extras()
    {
        var (ctx, resolver, _) = await SeedAsync(nameof(Test4_Item_count_matches_the_catalog_exactly_no_permission_generated_extras));
        var tree = await BuildTreeAsync(ctx, resolver, SystemRoles.MaintenanceEngineer, SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor);

        var cmms = tree.Single(m => m.ModuleKey == "CmmsIntegration");
        var totalItemNodes = cmms.Items.Count + cmms.Subgroups.Sum(s => s.Items.Count);

        Assert.Equal(NavigationCatalog.CmmsIntegrationItemOrder.Length, totalItemNodes);

        // بدون هیچ ItemKey تکراری، صرف‌نظر از این‌که هر آیتم چند Permission پشت‌اش دارد
        var allKeys = cmms.Items.Select(i => i.ItemKey).Concat(cmms.Subgroups.SelectMany(s => s.Items.Select(i => i.ItemKey)));
        Assert.Equal(allKeys.Count(), allKeys.Distinct().Count());
    }

    // ---------------------------------------------------------------------
    // Test 5 — Existing unmapped items (and the Equipment decision) remain
    // ---------------------------------------------------------------------
    [Fact]
    public void Test5_Instrument_Asset_SparePart_and_Equipment_remain_unmapped()
    {
        Assert.Null(NavigationCatalog.CmmsIntegrationItemResourceMap["Instrument"]);
        Assert.Null(NavigationCatalog.CmmsIntegrationItemResourceMap["Asset"]);
        Assert.Null(NavigationCatalog.CmmsIntegrationItemResourceMap["SparePart"]);
        Assert.Null(NavigationCatalog.CmmsIntegrationItemResourceMap["Equipment"]);
    }

    // ---------------------------------------------------------------------
    // Test 8 — Permission Matrix remains independent of Navigation Visibility
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Test8_Changing_PermissionMatrix_data_never_adds_or_removes_navigation_nodes()
    {
        var (ctx, resolver, permissionService) = await SeedAsync(nameof(Test8_Changing_PermissionMatrix_data_never_adds_or_removes_navigation_nodes));

        var role = new ApplicationRole("PermissionMatrixProbeRole") { NormalizedName = "PERMISSIONMATRIXPROBEROLE" };
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();

        var before = await BuildTreeAsync(ctx, resolver, "PermissionMatrixProbeRole");
        var beforeKeys = before.Select(m => m.ModuleKey)
            .Concat(before.SelectMany(m => m.Items.Select(i => i.ItemKey)))
            .Concat(before.SelectMany(m => m.Subgroups.SelectMany(s => s.Items.Select(i => i.ItemKey))))
            .OrderBy(k => k).ToList();

        // یک تغییر واقعی Permission Matrix - دقیقاً همان مسیری که صفحه‌ی Admin/PermissionMatrix صدا می‌زند
        await permissionService.SetPermissionsAsync("PermissionMatrixProbeRole", new[]
        {
            new ModulePermissionInput { ModuleName = "WorkOrder", CanView = true, CanCreate = true, CanApprove = true }
        });

        var after = await BuildTreeAsync(ctx, resolver, "PermissionMatrixProbeRole");
        var afterKeys = after.Select(m => m.ModuleKey)
            .Concat(after.SelectMany(m => m.Items.Select(i => i.ItemKey)))
            .Concat(after.SelectMany(m => m.Subgroups.SelectMany(s => s.Items.Select(i => i.ItemKey))))
            .OrderBy(k => k).ToList();

        // مجموعه‌ی گره‌های ناوبری کاملاً یکسان می‌ماند - فقط EffectiveVisible داخل WorkOrder تغییر می‌کند
        Assert.Equal(beforeKeys, afterKeys);

        var workOrderBefore = before.Single(m => m.ModuleKey == "CmmsIntegration").Items.Single(i => i.ItemKey == "WorkOrder");
        var workOrderAfter = after.Single(m => m.ModuleKey == "CmmsIntegration").Items.Single(i => i.ItemKey == "WorkOrder");
        Assert.False(workOrderBefore.RoleAccess.Single().EffectiveVisible, "Before granting, the probe role must not see WorkOrder.");
        Assert.True(workOrderAfter.RoleAccess.Single().EffectiveVisible, "After granting via Permission Matrix, the probe role must see WorkOrder - proving the grant flows through, not that it created a new node.");
    }
}
