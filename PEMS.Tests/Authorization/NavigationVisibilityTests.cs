using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Security;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق سند «PEMS Phase 6 — User Module & Navigation Visibility Architecture»: پوشش
/// NavigationVisibilityResolver - پیش‌فرض (MenuAccess/مجوزمحور)، Override دستی، و معناشناسی
/// اجتماع قطعی چند-نقشی. عمداً بدون Mock روی INavigationVisibilityOverrideService/
/// IAccessControlService - دقیقاً هم‌الگو با AuthorizationArchitectureCompletionTests (Seed واقعی
/// روی یک PEMSDbContext درون‌حافظه‌ای تازه).
/// </summary>
public class NavigationVisibilityTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, NavigationVisibilityResolver resolver, NavigationVisibilityOverrideService overrides)> SeedAsync(string dbName)
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
        return (ctx, resolver, overrides);
    }

    [Fact]
    public async Task Module_visibility_reproduces_MenuAccess_by_default_with_no_overrides()
    {
        var (_, resolver, _) = await SeedAsync(nameof(Module_visibility_reproduces_MenuAccess_by_default_with_no_overrides));

        // طبق MenuAccess.CmmsIntegration: SystemAdmin/EngineeringManager/EngineeringSupervisor/MaintenanceEngineer می‌بینند، QAManager نه
        var snapshotEngManager = await resolver.ResolveAsync(new[] { SystemRoles.EngineeringManager });
        Assert.True(snapshotEngManager.IsModuleVisible("CmmsIntegration"));

        var snapshotQa = await resolver.ResolveAsync(new[] { SystemRoles.QAManager });
        Assert.False(snapshotQa.IsModuleVisible("CmmsIntegration"));
    }

    [Fact]
    public async Task Explicit_hide_override_hides_a_module_that_MenuAccess_would_otherwise_show()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Explicit_hide_override_hides_a_module_that_MenuAccess_would_otherwise_show));
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.MaintenanceEngineer).Select(r => r.Id).FirstAsync();

        Assert.True((await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer })).IsModuleVisible("CmmsIntegration"));

        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", null, isVisible: false, reason: "test", actingUser: "test");

        Assert.False((await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer })).IsModuleVisible("CmmsIntegration"));
    }

    [Fact]
    public async Task Explicit_show_override_reveals_a_module_that_MenuAccess_would_otherwise_hide()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Explicit_show_override_reveals_a_module_that_MenuAccess_would_otherwise_hide));
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.QAManager).Select(r => r.Id).FirstAsync();

        Assert.False((await resolver.ResolveAsync(new[] { SystemRoles.QAManager })).IsModuleVisible("CmmsIntegration"));

        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", null, isVisible: true, reason: "test", actingUser: "test");

        Assert.True((await resolver.ResolveAsync(new[] { SystemRoles.QAManager })).IsModuleVisible("CmmsIntegration"));
    }

    [Fact]
    public async Task Clearing_an_override_reverts_to_default_visibility()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Clearing_an_override_reverts_to_default_visibility));
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.MaintenanceEngineer).Select(r => r.Id).FirstAsync();

        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", null, isVisible: false, reason: "test", actingUser: "test");
        Assert.False((await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer })).IsModuleVisible("CmmsIntegration"));

        await overrides.ClearOverrideAsync(roleId, "CmmsIntegration", null);
        Assert.True((await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer })).IsModuleVisible("CmmsIntegration"));
    }

    [Fact]
    public async Task Multi_role_union_a_hide_override_on_one_role_never_hides_it_for_a_user_whose_other_role_still_defaults_visible()
    {
        // طبق بند «معناشناسی اجتماع قطعی چند-نقشی (بدون Deny-Override دلبخواهی)»: کاربری با دو
        // نقش - یکی Override مخفی خورده، دیگری هنوز پیش‌فرض نمایان است - باید همچنان ببیند.
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Multi_role_union_a_hide_override_on_one_role_never_hides_it_for_a_user_whose_other_role_still_defaults_visible));
        var maintenanceEngineerId = await ctx.Roles.Where(r => r.Name == SystemRoles.MaintenanceEngineer).Select(r => r.Id).FirstAsync();

        await overrides.SetOverrideAsync(maintenanceEngineerId, "CmmsIntegration", null, isVisible: false, reason: "test", actingUser: "test");

        // به‌تنهایی این نقش دیگر نباید ببیند
        Assert.False((await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer })).IsModuleVisible("CmmsIntegration"));

        // ولی کاربری با هر دو نقش (MaintenanceEngineer مخفی‌شده + EngineeringManager پیش‌فرض نمایان) باید ببیند
        var combined = await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer, SystemRoles.EngineeringManager });
        Assert.True(combined.IsModuleVisible("CmmsIntegration"));
    }

    [Fact]
    public async Task CmmsIntegration_item_with_a_real_permission_grant_is_visible()
    {
        // WorkOrder واقعاً به MaintenanceEngineer Grant دارد (PermissionService.SeedDefaultsAsync)
        var (_, resolver, _) = await SeedAsync(nameof(CmmsIntegration_item_with_a_real_permission_grant_is_visible));

        var snapshot = await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer });
        Assert.True(snapshot.IsModuleVisible("CmmsIntegration"));
        Assert.True(snapshot.IsCmmsItemVisible("WorkOrder"));
    }

    [Fact]
    public async Task CmmsIntegration_item_is_hidden_for_a_role_with_no_grant_for_its_mapped_resource()
    {
        // طبق DecideAsync: بدون هیچ RolePermissionGrant فعالی برای «WorkOrder.View»، تصمیم Deny است -
        // این‌جا با ساخت یک نقش تازه (بدون هیچ Grant/Seed) دقیقاً همین حالت را اثبات می‌کنیم،
        // به‌جای فرض کردن این‌که کدام Resource واقعی از قبل بدون Grant مانده (که بین Seed های
        // مختلف فرق می‌کند).
        var (ctx, resolver, _) = await SeedAsync(nameof(CmmsIntegration_item_is_hidden_for_a_role_with_no_grant_for_its_mapped_resource));

        var blankRole = new ApplicationRole("BlankCmmsRole") { NormalizedName = "BLANKCMMSROLE" };
        ctx.Roles.Add(blankRole);
        await ctx.SaveChangesAsync();

        var roleId = blankRole.Id;
        var overrides = new NavigationVisibilityOverrideService(ctx);
        // این نقش تازه به‌تنهایی ماژول CmmsIntegration را هم نمی‌بیند (نه در MenuAccess است، نه Override دارد) -
        // برای اثبات دقیق «آیتم بدون Grant مخفی است»، ماژول را صریحاً برای همین نقش نمایان می‌کنیم.
        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", null, isVisible: true, reason: "test", actingUser: "test");

        var snapshot = await resolver.ResolveAsync(new[] { "BlankCmmsRole" });
        Assert.True(snapshot.IsModuleVisible("CmmsIntegration"));
        Assert.False(snapshot.IsCmmsItemVisible("WorkOrder"), "A role with zero RolePermissionGrant for WorkOrder.View must not see the WorkOrder item.");
    }

    [Fact]
    public async Task CmmsIntegration_item_without_a_mapped_resource_is_always_visible_when_the_module_is_visible()
    {
        // Instrument/Asset/SparePart عمداً بدون Resource نگاشته‌شده‌اند (بدون سیگنال مجوزی واقعی)
        var (_, resolver, _) = await SeedAsync(nameof(CmmsIntegration_item_without_a_mapped_resource_is_always_visible_when_the_module_is_visible));

        var snapshot = await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer });
        Assert.True(snapshot.IsCmmsItemVisible("Instrument"));
        Assert.True(snapshot.IsCmmsItemVisible("Asset"));
        Assert.True(snapshot.IsCmmsItemVisible("SparePart"));
    }

    [Fact]
    public async Task Item_level_override_can_hide_a_single_item_without_hiding_the_rest_of_the_module()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Item_level_override_can_hide_a_single_item_without_hiding_the_rest_of_the_module));
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.MaintenanceEngineer).Select(r => r.Id).FirstAsync();

        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", "WorkOrder", isVisible: false, reason: "test", actingUser: "test");

        var snapshot = await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer });
        Assert.True(snapshot.IsModuleVisible("CmmsIntegration"));
        Assert.False(snapshot.IsCmmsItemVisible("WorkOrder"));
        Assert.True(snapshot.IsCmmsItemVisible("WorkRequest"));
    }

    // ==============================================================================
    // Hidden ≠ Unauthorized (طبق سند فاز ۶، بند صریح): مخفی‌کردن یک آیتم از طریق
    // NavigationVisibilityOverride هرگز نباید بر تصمیم واقعی AccessControlService اثر بگذارد -
    // این جدول فقط توسط NavigationVisibilityResolver خوانده می‌شود، هرگز توسط DecideAsync. اگر
    // این تست شکست بخورد، یعنی Visibility به‌طور تصادفی یک لایه‌ی Authorization دوم شده - دقیقاً
    // همان ضد-الگویی که کل این فاز صریحاً ممنوعش کرده.
    // ==============================================================================

    [Fact]
    public async Task Hiding_an_item_via_NavigationVisibilityOverride_never_changes_the_real_authorization_decision()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Hiding_an_item_via_NavigationVisibilityOverride_never_changes_the_real_authorization_decision));
        var access = new AccessControlService(ctx);
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.MaintenanceEngineer).Select(r => r.Id).FirstAsync();

        var beforeDecision = await access.AuthorizeForRolesAsync(new[] { SystemRoles.MaintenanceEngineer }, "WorkOrder", "View");
        Assert.True(beforeDecision.Allowed);

        // مدیر آیتم WorkOrder را برای این نقش، هم در سطح ماژول و هم در سطح آیتم، صریحاً مخفی می‌کند
        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", null, isVisible: false, reason: "test", actingUser: "test");
        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", "WorkOrder", isVisible: false, reason: "test", actingUser: "test");

        // سایدبار اکنون واقعاً هیچ‌کدام را نشان نمی‌دهد
        var snapshot = await resolver.ResolveAsync(new[] { SystemRoles.MaintenanceEngineer });
        Assert.False(snapshot.IsModuleVisible("CmmsIntegration"));

        // ولی تصمیم واقعی مجوزدهی روی WorkOrder.View - همانی که خودِ WorkOrderController واقعاً
        // برای اجازه‌دادن/رد‌کردن یک درخواست HTTP واقعی صدا می‌زند - باید کاملاً دست‌نخورده بماند
        var afterDecision = await access.AuthorizeForRolesAsync(new[] { SystemRoles.MaintenanceEngineer }, "WorkOrder", "View");
        Assert.True(afterDecision.Allowed, "Hiding a sidebar item must never deny the underlying permission - Hidden is not Unauthorized.");
        Assert.Equal(beforeDecision.Reason, afterDecision.Reason);
    }

    // ==============================================================================
    // Visible ≠ Authorized (جهت معکوس همان بند): نمایان‌کردن یک آیتم از طریق Override دستی هم
    // به همان اندازه نباید بر تصمیم واقعی اثر بگذارد - یک نقش با Override نمایان اما بدون
    // Grant واقعی، باید هنوز هم از دسترسی واقعی رد شود (لینک ممکن است دیده شود، ولی پشتش
    // همچنان Forbid است) - دقیقاً همان الگویی که سه فایل تست Navigation Exposure پیشین برای
    // MenuAccess اثبات کرده بودند، این‌جا برای Override تازه.
    // ==============================================================================

    [Fact]
    public async Task Showing_an_item_via_NavigationVisibilityOverride_never_grants_the_real_authorization_decision()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(Showing_an_item_via_NavigationVisibilityOverride_never_grants_the_real_authorization_decision));
        var access = new AccessControlService(ctx);
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.QAManager).Select(r => r.Id).FirstAsync();

        var beforeDecision = await access.AuthorizeForRolesAsync(new[] { SystemRoles.QAManager }, "WorkOrder", "View");
        Assert.False(beforeDecision.Allowed);

        // مدیر آیتم WorkOrder را برای QAManager، هم در سطح ماژول و هم در سطح آیتم، صریحاً نمایان می‌کند
        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", null, isVisible: true, reason: "test", actingUser: "test");
        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", "WorkOrder", isVisible: true, reason: "test", actingUser: "test");

        // سایدبار اکنون واقعاً هردو را نشان می‌دهد
        var snapshot = await resolver.ResolveAsync(new[] { SystemRoles.QAManager });
        Assert.True(snapshot.IsModuleVisible("CmmsIntegration"));
        Assert.True(snapshot.IsCmmsItemVisible("WorkOrder"));

        // ولی تصمیم واقعی مجوزدهی همچنان رد است - دیده‌شدن یک لینک هرگز مجوز واقعی اعطا نمی‌کند
        var afterDecision = await access.AuthorizeForRolesAsync(new[] { SystemRoles.QAManager }, "WorkOrder", "View");
        Assert.False(afterDecision.Allowed, "Showing a sidebar item must never grant the underlying permission - Visible is not Authorized.");
    }

    [Fact]
    public async Task DescribeForRoleAsync_reports_default_override_and_effective_separately()
    {
        var (ctx, resolver, overrides) = await SeedAsync(nameof(DescribeForRoleAsync_reports_default_override_and_effective_separately));
        var roleId = await ctx.Roles.Where(r => r.Name == SystemRoles.MaintenanceEngineer).Select(r => r.Id).FirstAsync();

        await overrides.SetOverrideAsync(roleId, "CmmsIntegration", "WorkOrder", isVisible: false, reason: "test", actingUser: "test");

        var rows = await resolver.DescribeForRoleAsync(SystemRoles.MaintenanceEngineer);
        var workOrderRow = rows.Single(r => r.ModuleKey == "CmmsIntegration" && r.ItemKey == "WorkOrder");

        Assert.True(workOrderRow.DefaultVisible, "Default (pre-override) should still reflect the real WorkOrder.View grant.");
        Assert.Equal(false, workOrderRow.OverrideVisible);
        Assert.False(workOrderRow.EffectiveVisible, "Effective must be the override, not the default.");
        Assert.True(workOrderRow.HasOverride);
    }
}
