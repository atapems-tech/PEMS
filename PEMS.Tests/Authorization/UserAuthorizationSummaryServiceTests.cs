using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق «PEMS — Enterprise User-Centric Authorization Workspace Consolidation»: پوشش سرویس تازه‌ی
/// این فاز. GetEffectivePermissionsForRolesAsync باید دقیقاً همان نتیجه‌ای را بدهد که قبلاً منطق
/// خصوصی AuthorizationController.BuildRoleRowsAsync می‌داد (اثبات Regression = صفر، طبق §30 سند) -
/// اکنون هر دو صفحه‌ی قدیمی (Admin/Authorization) و صفحه‌ی تازه (Admin/Users/Details) از همین یک
/// متد می‌خوانند. GetSoDExposureForRolesAsync ترکیب دو منبع موجود است (SoDPolicies فعال +
/// AuthorizeForRolesAsync) - نه یک موتور SoD تازه؛ نباید «Conflict» قطعی گزارش کند (طبق DTO -
/// فقط «Exposure»).
/// </summary>
public class UserAuthorizationSummaryServiceTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, UserAuthorizationSummaryService summary, AccessControlService access)> SeedAsync(string dbName)
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

        var summary = new UserAuthorizationSummaryService(ctx, access);
        return (ctx, summary, access);
    }

    // ---------------------------------------------------------------------
    // Parity: GetEffectivePermissionsForRolesAsync must reproduce exactly what
    // AccessControlService.AuthorizeForRolesAsync already returns per permission - no drift
    // introduced by relocating the logic out of AuthorizationController.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task GetEffectivePermissionsForRolesAsync_Matches_AuthorizeForRolesAsync_PerPermission()
    {
        var (ctx, summary, access) = await SeedAsync(nameof(GetEffectivePermissionsForRolesAsync_Matches_AuthorizeForRolesAsync_PerPermission));
        var roles = new[] { SystemRoles.MaintenanceEngineer, SystemRoles.EngineeringManager };

        var rows = await summary.GetEffectivePermissionsForRolesAsync(roles);
        Assert.NotEmpty(rows);

        var permissions = await ctx.Permissions.Where(p => p.IsActive).ToListAsync();
        Assert.Equal(permissions.Count, rows.Count);

        foreach (var row in rows)
        {
            var decision = await access.AuthorizeForRolesAsync(roles, row.ResourceName, row.ActionName);
            Assert.Equal(decision.Allowed, row.Allowed);
        }
    }

    [Fact]
    public async Task GetEffectivePermissionsForRolesAsync_EmptyRoleList_DeniesEverything()
    {
        var (_, summary, _) = await SeedAsync(nameof(GetEffectivePermissionsForRolesAsync_EmptyRoleList_DeniesEverything));

        var rows = await summary.GetEffectivePermissionsForRolesAsync(Array.Empty<string>());

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.False(r.Allowed));
    }

    // ---------------------------------------------------------------------
    // SoD exposure: read-only composition, never a fabricated "Conflict" status.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task GetSoDExposureForRolesAsync_FlagsExposure_WhenRoleGrantsBothSidesOfAnActivePolicy()
    {
        var (ctx, summary, _) = await SeedAsync(nameof(GetSoDExposureForRolesAsync_FlagsExposure_WhenRoleGrantsBothSidesOfAnActivePolicy));

        // یک سیاست SoD واقعی روی دو Permission واقعی موجود (WorkOrder.View / WorkOrder.Approve)،
        // هر دو به EngineeringManager Grant شده (طبق SeedDefaultsAsync) - دقیقاً حالت Exposure
        var permView = await ctx.Permissions.SingleAsync(p => p.ResourceName == "WorkOrder" && p.ActionName == "View");
        var permApprove = await ctx.Permissions.SingleAsync(p => p.ResourceName == "WorkOrder" && p.ActionName == "Approve");
        ctx.SoDPolicies.Add(new SoDPolicy { PermissionAId = permView.PermissionId, PermissionBId = permApprove.PermissionId, IsActive = true, Description = "test policy", CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var exposure = await summary.GetSoDExposureForRolesAsync(new[] { SystemRoles.EngineeringManager });

        var row = Assert.Single(exposure);
        Assert.True(row.UserHoldsBoth, "EngineeringManager holds both WorkOrder.View and WorkOrder.Approve - must be flagged as exposed.");
    }

    [Fact]
    public async Task GetSoDExposureForRolesAsync_DoesNotFlag_WhenRoleOnlyHoldsOneSide()
    {
        var (ctx, summary, _) = await SeedAsync(nameof(GetSoDExposureForRolesAsync_DoesNotFlag_WhenRoleOnlyHoldsOneSide));

        var permView = await ctx.Permissions.SingleAsync(p => p.ResourceName == "WorkOrder" && p.ActionName == "View");
        var permApprove = await ctx.Permissions.SingleAsync(p => p.ResourceName == "WorkOrder" && p.ActionName == "Approve");
        ctx.SoDPolicies.Add(new SoDPolicy { PermissionAId = permView.PermissionId, PermissionBId = permApprove.PermissionId, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        // طبق SeedDefaultsAsync: DisciplineEngineer فقط View و Approve را روی WorkOrder دارد؟
        // در واقع Viewer فقط View دارد، نه Approve - دقیقاً «یک طرف فقط»
        var exposure = await summary.GetSoDExposureForRolesAsync(new[] { SystemRoles.Viewer });

        var row = Assert.Single(exposure);
        Assert.False(row.UserHoldsBoth, "Viewer only holds WorkOrder.View, not Approve - must not be flagged as exposed.");
    }

    [Fact]
    public async Task GetSoDExposureForRolesAsync_IgnoresInactivePolicies()
    {
        var (ctx, summary, _) = await SeedAsync(nameof(GetSoDExposureForRolesAsync_IgnoresInactivePolicies));

        var permView = await ctx.Permissions.SingleAsync(p => p.ResourceName == "WorkOrder" && p.ActionName == "View");
        var permApprove = await ctx.Permissions.SingleAsync(p => p.ResourceName == "WorkOrder" && p.ActionName == "Approve");
        ctx.SoDPolicies.Add(new SoDPolicy { PermissionAId = permView.PermissionId, PermissionBId = permApprove.PermissionId, IsActive = false, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var exposure = await summary.GetSoDExposureForRolesAsync(new[] { SystemRoles.EngineeringManager });

        Assert.Empty(exposure);
    }

    [Fact]
    public async Task GetSoDExposureForRolesAsync_NoActivePolicies_ReturnsEmpty()
    {
        var (_, summary, _) = await SeedAsync(nameof(GetSoDExposureForRolesAsync_NoActivePolicies_ReturnsEmpty));

        var exposure = await summary.GetSoDExposureForRolesAsync(new[] { SystemRoles.EngineeringManager });

        Assert.Empty(exposure);
    }
}
