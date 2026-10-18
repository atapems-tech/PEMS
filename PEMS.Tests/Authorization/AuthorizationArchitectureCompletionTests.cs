using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS Authorization Architecture Completion» فاز ۳/۴: پوشش تازه برای دقیقاً همان مسیرهایی
/// که ممیزی قبلی نشان داد هیچ تستی نداشتند (RolesController.Create → هیچ Grant خودکاری، و
/// SetPermissionsAsync → هم‌گام‌سازی فوری RolePermissionGrant بدون Restart، شامل مسیر لغو که
/// تازه اضافه شده). AccessControlParityTests/AccessControlScopeAndSoDTests موجود دست‌نخورده
/// می‌مانند - این فایل مکملشان است، نه جایگزین.
/// </summary>
public class AuthorizationArchitectureCompletionTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, AccessControlService access, PermissionService permissionService)> SeedAsync(string dbName)
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

        return (ctx, access, permissionService);
    }

    [Fact]
    public async Task Creating_a_role_grants_nothing_and_it_stays_denied_until_explicitly_configured()
    {
        var (ctx, access, _) = await SeedAsync(nameof(Creating_a_role_grants_nothing_and_it_stays_denied_until_explicitly_configured));

        // دقیقاً همان کاری که RolesController.Create انجام می‌دهد: فقط INSERT روی AspNetRoles
        var role = new ApplicationRole("maintenance planning") { NormalizedName = "MAINTENANCE PLANNING" };
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();

        Assert.False(await ctx.RolePermissionGrants.AnyAsync(g => g.RoleId == role.Id));

        var decision = await access.AuthorizeForRolesAsync(new[] { "maintenance planning" }, "WorkOrder", "View");
        Assert.False(decision.Allowed);
        Assert.Equal(AccessDecisionReason.DeniedByRole, decision.Reason);
    }

    [Fact]
    public async Task Granting_a_permission_through_SetPermissionsAsync_takes_effect_immediately_without_a_restart()
    {
        var (ctx, access, permissionService) = await SeedAsync(nameof(Granting_a_permission_through_SetPermissionsAsync_takes_effect_immediately_without_a_restart));

        var role = new ApplicationRole("QualityInspector") { NormalizedName = "QUALITYINSPECTOR" };
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();

        Assert.False((await access.AuthorizeForRolesAsync(new[] { "QualityInspector" }, "Inspection", "View")).Allowed);

        // دقیقاً همان مسیری که Admin/PermissionMatrix هنگام ذخیره صدا می‌زند
        await permissionService.SetPermissionsAsync("QualityInspector", new[]
        {
            new ModulePermissionInput { ModuleName = "Inspection", CanView = true, CanCreate = true }
        });

        // بدون هیچ فراخوان دیگری (بدون شبیه‌سازی Restart/AccessControlSeeder دوباره) - باید همین الان منعکس شود
        var decision = await access.AuthorizeForRolesAsync(new[] { "QualityInspector" }, "Inspection", "View");
        Assert.True(decision.Allowed, decision.Detail);
        Assert.True((await access.AuthorizeForRolesAsync(new[] { "QualityInspector" }, "Inspection", "Create")).Allowed);
        Assert.False((await access.AuthorizeForRolesAsync(new[] { "QualityInspector" }, "Inspection", "Approve")).Allowed);
    }

    [Fact]
    public async Task Revoking_a_permission_through_SetPermissionsAsync_denies_immediately_without_a_restart()
    {
        var (ctx, access, permissionService) = await SeedAsync(nameof(Revoking_a_permission_through_SetPermissionsAsync_denies_immediately_without_a_restart));

        var role = new ApplicationRole("QualityInspector2") { NormalizedName = "QUALITYINSPECTOR2" };
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();

        await permissionService.SetPermissionsAsync("QualityInspector2", new[]
        {
            new ModulePermissionInput { ModuleName = "Inspection", CanView = true, CanCreate = true }
        });
        Assert.True((await access.AuthorizeForRolesAsync(new[] { "QualityInspector2" }, "Inspection", "Create")).Allowed);

        // مدیر Checkbox «Create» را برمی‌دارد - این دقیقاً نقطه‌ای است که پیش از این تغییر، Grant
        // قدیمی برای همیشه فعال می‌ماند (فقط GrantIfTrueAsync، بدون مسیر لغو)
        await permissionService.SetPermissionsAsync("QualityInspector2", new[]
        {
            new ModulePermissionInput { ModuleName = "Inspection", CanView = true, CanCreate = false }
        });

        var decision = await access.AuthorizeForRolesAsync(new[] { "QualityInspector2" }, "Inspection", "Create");
        Assert.False(decision.Allowed, "Revoking a checkbox must actually deny the runtime decision, not just leave a stale grant active.");
        Assert.True((await access.AuthorizeForRolesAsync(new[] { "QualityInspector2" }, "Inspection", "View")).Allowed, "Revoking Create must not accidentally also revoke View.");
    }

    [Fact]
    public async Task EngineeringManager_can_approve_any_discipline_but_EngineeringSupervisor_cannot()
    {
        var (_, access, _) = await SeedAsync(nameof(EngineeringManager_can_approve_any_discipline_but_EngineeringSupervisor_cannot));

        Assert.True((await access.AuthorizeForRolesAsync(new[] { SystemRoles.EngineeringManager }, "EngineeringDocuments", "Approve")).Allowed);
        Assert.True((await access.AuthorizeForRolesAsync(new[] { SystemRoles.EngineeringManager }, "EngineeringDocuments", "ApproveAllDisciplines")).Allowed);

        Assert.True((await access.AuthorizeForRolesAsync(new[] { SystemRoles.EngineeringSupervisor }, "EngineeringDocuments", "Approve")).Allowed);
        Assert.False((await access.AuthorizeForRolesAsync(new[] { SystemRoles.EngineeringSupervisor }, "EngineeringDocuments", "ApproveAllDisciplines")).Allowed);

        Assert.False((await access.AuthorizeForRolesAsync(new[] { SystemRoles.DisciplineEngineer }, "EngineeringDocuments", "Approve")).Allowed);
    }
}
