using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Authorization;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture (فاز ۱): این تست‌ها ثابت می‌کنند
/// AccessControlService رفتار PermissionService/RoleModuleActionService موجود را عیناً بازتولید
/// می‌کند - نه با مقایسه‌ی HasPermissionAsync با خودش (که چون HasPermissionAsync اکنون به همین
/// AccessControlService واگذار شده، بی‌معنی می‌شد)، بلکه با محاسبه‌ی مستقل «مقدار انتظار» از
/// روی ستون‌های خام RolePermission/RoleModuleAction (منطقی که خودِ HasPermissionAsync قدیمی قبل
/// از این تغییر داشت). داده‌ی Seed واقعی مصرف می‌شود (نه Fixture دستی) تا هر افزوده‌ی آینده به
/// RolePermission/RoleModuleAction خودکار پوشش داده شود.
/// </summary>
public class AccessControlParityTests
{
    private static readonly string[] FixedActions =
        { "View", "Create", "Edit", "Delete", "Approve", "ManualNumber", "Freeze", "Unfreeze", "Hold", "ReleaseHold" };

    private static async Task<(PEMS.Persistence.Context.PEMSDbContext ctx, AccessControlService accessControl,
        PermissionService permissionService, RoleModuleActionService roleModuleActionService)> SeedAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);

        foreach (var roleName in SystemRoles.All)
            ctx.Roles.Add(new ApplicationRole(roleName) { NormalizedName = roleName.ToUpperInvariant() });
        await ctx.SaveChangesAsync();

        var accessControl = new AccessControlService(ctx);
        var permissionService = new PermissionService(ctx, accessControl);
        var roleModuleActionService = new RoleModuleActionService(ctx, accessControl);

        await permissionService.SeedDefaultsAsync();
        await roleModuleActionService.SeedDefaultsAsync();

        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        return (ctx, accessControl, permissionService, roleModuleActionService);
    }

    [Fact]
    public async Task RolePermissions_EveryRoleModuleActionCombo_AccessControlAgreesWithRawBooleanGrid()
    {
        var (ctx, accessControl, _, _) = await SeedAsync(
            nameof(RolePermissions_EveryRoleModuleActionCombo_AccessControlAgreesWithRawBooleanGrid));

        var rows = await ctx.RolePermissions.ToListAsync();
        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            foreach (var action in FixedActions)
            {
                // «مقدار انتظار» عیناً همون Switch قدیمی HasPermissionAsync، مستقل از پیاده‌سازی جدید
                var expected = action switch
                {
                    "View" => row.CanView,
                    "Create" => row.CanCreate,
                    "Edit" => row.CanEdit,
                    "Delete" => row.CanDelete,
                    "Approve" => row.CanApprove,
                    "ManualNumber" => row.CanManualNumber,
                    "Freeze" => row.CanFreeze,
                    "Unfreeze" => row.CanUnfreeze,
                    "Hold" => row.CanHold,
                    "ReleaseHold" => row.CanReleaseHold,
                    _ => false
                };

                var actual = (await accessControl.AuthorizeForRolesAsync(new[] { row.RoleName }, row.ModuleName, action)).Allowed;

                Assert.True(expected == actual,
                    $"Parity mismatch: Role={row.RoleName} Module={row.ModuleName} Action={action} expected={expected} actual={actual}");
            }
        }
    }

    [Fact]
    public async Task RoleModuleActions_EveryRow_AccessControlAgreesWithRawTable()
    {
        var (ctx, accessControl, _, _) = await SeedAsync(nameof(RoleModuleActions_EveryRow_AccessControlAgreesWithRawTable));

        var rows = await ctx.RoleModuleActions.ToListAsync();
        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            var actual = (await accessControl.AuthorizeForRolesAsync(new[] { row.RoleName }, row.Module, row.Action)).Allowed;
            Assert.True(actual, $"Expected Allow for seeded row Role={row.RoleName} Module={row.Module} Action={row.Action} but got Deny");
        }
    }

    [Fact]
    public async Task SystemAdmin_BypassesEveryModuleAction_ExceptExecutionGateEmergencyOverride()
    {
        var (_, _, permissionService, roleModuleActionService) = await SeedAsync(
            nameof(SystemAdmin_BypassesEveryModuleAction_ExceptExecutionGateEmergencyOverride));

        // یک ماژول/اکشن دلخواه که SystemAdmin برایش Grant صریح ندارد - Bypass عمومی باید کار کند
        var allowed = await permissionService.HasPermissionAsync(new[] { SystemRoles.SystemAdmin }, "EngineeringDocuments", "Delete");
        Assert.True(allowed);

        // طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۱۲: این یکی استثنای عمدی است - نباید Bypass شود
        var breakGlass = await roleModuleActionService.HasActionAsync(new[] { SystemRoles.SystemAdmin }, "ExecutionGate", "EmergencyOverride");
        Assert.False(breakGlass);
    }

    [Fact]
    public async Task NoOrphanGrants_EveryGrantedCombinationResolvesToARealPermission()
    {
        var (ctx, _, _, _) = await SeedAsync(nameof(NoOrphanGrants_EveryGrantedCombinationResolvesToARealPermission));

        var grants = await ctx.RolePermissionGrants.Include(g => g.Permission).ToListAsync();
        Assert.NotEmpty(grants);
        Assert.All(grants, g => Assert.NotNull(g.Permission));
    }

    [Fact]
    public async Task AuthorizeAsync_WritesAuthorizationAuditLogRow()
    {
        var (ctx, accessControl, _, _) = await SeedAsync(nameof(AuthorizeAsync_WritesAuthorizationAuditLogRow));

        var role = ctx.Roles.First(r => r.Name == SystemRoles.Viewer);
        var user = new ApplicationUser { UserName = "audit.test", Email = "audit.test@pems.local", FullName = "Audit Test" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        await accessControl.AuthorizeAsync(new PEMS.Application.DTOs.AccessControlRequest
        {
            UserId = user.Id,
            ResourceName = "EngineeringDocuments",
            ActionName = "View"
        });

        var logCount = await ctx.AuthorizationAuditLogs.CountAsync(x => x.UserId == user.Id);
        Assert.Equal(1, logCount);
    }
}
