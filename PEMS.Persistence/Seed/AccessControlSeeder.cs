using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture (فاز ۱): داده‌ی امروزِ RolePermission/
/// RoleModuleAction/AspNetUserRoles را به مدل عمومی جدید (Permission/RolePermissionGrant/
/// RoleAssignment) کپی می‌کند - جدول‌های منبع دست‌نخورده باقی می‌مانند (فقط خوانده می‌شوند).
/// سه گام مستقل، هرکدام Idempotent به‌ازای خودش - دقیقاً همون درسی که در PermissionService
/// (بند WorkOrder/StoreRequisition/...) و RoleModuleActionService (SeedModuleAsync) قبلاً اعمال
/// شده: یک گارد سراسری «اگه هر ردیفی هست کلاً رد شو» باعث می‌شود داده‌ی بعداً اضافه‌شده به منبع
/// (مثلاً یک ماژول جدید در RolePermission) هیچ‌وقت به این‌جا هم‌گام نشود.
/// </summary>
public static class AccessControlSeeder
{
    // طبق بند ۷ سند: این اکشن‌ها همیشه باید صریح باشند - علامت‌گذاری IsDestructive برای
    // مصرف فازهای بعدی (Simulator/Effective Permissions)؛ در فاز ۱ چیزی را مسدود نمی‌کند.
    private static readonly HashSet<string> DestructiveActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Delete", "HardDelete", "Cancel", "Reject", "Approve", "Issue", "Unissue", "Close",
        "Reopen", "Freeze", "Unfreeze", "Override", "EmergencyOverride", "Hold", "ReleaseHold",
        "Revoke", "Isolate", "Deisolate", "Suspend", "Resume"
    };

    public static async Task SeedFromLegacyAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        var permissionCache = new Dictionary<string, Permission>(StringComparer.Ordinal);

        await SeedFromRolePermissionsAsync(db, permissionCache);
        await SeedFromRoleModuleActionsAsync(db, permissionCache);
        await SeedRoleAssignmentsFromUserRolesAsync(db);
    }

    /// <summary>
    /// طبق سند RBAC-ABAC Authorization Architecture (فاز ۳ - جایگزینی مکانیزم D): این دو مجوز
    /// از هیچ جدول Legacy مشتق نشده‌اند (بر خلاف SeedFromLegacyAsync) - معادل داده‌محور دقیق دو
    /// آرایه‌ی ثابت (Hard-Coded) که تا امروز مستقیماً در کد تکرار شده بودند:
    /// «EngineeringDocuments.ViewAllDisciplines» = دقیقاً همان UnrestrictedRoles که در سه فایل
    /// (EngineeringDocumentsController, EngineeringDashboardController, DocumentRegisterController)
    /// عیناً کپی شده بود؛ «Document.RecordClientResponse» = دقیقاً همان ClientResponseRoles در
    /// DocumentWorkflowService. SystemAdmin عمداً این‌جا صریح Grant نمی‌شود - طبق الگوی موجود
    /// پروژه (RolePermission/RoleModuleAction هم هرگز SystemAdmin را صریح لیست نمی‌کنند)، از
    /// Bypass عمومی AccessControlService.DecideAsync/AuthorizeForRolesAsync استفاده می‌کند.
    /// </summary>
    public static async Task SeedPhase3PermissionsAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        var cache = new Dictionary<string, Permission>(StringComparer.Ordinal);

        foreach (var roleName in new[]
        {
            PEMS.Persistence.Identity.SystemRoles.EngineeringManager,
            PEMS.Persistence.Identity.SystemRoles.EngineeringSupervisor,
            PEMS.Persistence.Identity.SystemRoles.DocumentController
        })
            await GrantIfTrueAsync(db, cache, roleName, "EngineeringDocuments", "ViewAllDisciplines", true);

        foreach (var roleName in new[]
        {
            PEMS.Persistence.Identity.SystemRoles.EngineeringManager,
            PEMS.Persistence.Identity.SystemRoles.DocumentController
        })
            await GrantIfTrueAsync(db, cache, roleName, "Document", "RecordClientResponse", true);
    }

    /// <summary>
    /// شروع فاز ۵ (Governance): سیاست‌های SoD پیش‌فرض برای جلوگیری از خودتاییدی و کنترل‌های
    /// حساس اجرایی. این‌ها فقط Seed هستند و در آینده از پنل Admin قابل مدیریت می‌شوند.
    /// </summary>
    public static async Task SeedPhase5GovernanceAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        var cache = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);

        var defaultPolicies = new[]
        {
            new { FirstResource = "EngineeringDocuments", FirstAction = "Create", SecondResource = "EngineeringDocuments", SecondAction = "Approve", Description = "کاربر نمی‌تواند مدرک را خودش ایجاد و سپس همان رکورد را تایید کند." },
            new { FirstResource = "EngineeringDocuments", FirstAction = "Edit", SecondResource = "EngineeringDocuments", SecondAction = "Approve", Description = "کاربر نمی‌تواند همان مدرک را ویرایش و سپس تأیید کند." },
            new { FirstResource = "WorkOrder", FirstAction = "Create", SecondResource = "WorkOrder", SecondAction = "Close", Description = "ایجاد و بستن همان WorkOrder توسط یک نفر ممنوع است." },
            new { FirstResource = "WorkOrder", FirstAction = "Edit", SecondResource = "WorkOrder", SecondAction = "Close", Description = "ویرایش و بستن همان WorkOrder توسط یک نفر ممنوع است." },
            new { FirstResource = "Permit", FirstAction = "Create", SecondResource = "Permit", SecondAction = "Issue", Description = "نمی‌توان یک Permit را ایجاد و در همان لحظه صادر کرد." },
            // طبق «PEMS Operations & Production Architecture Spec v1» فاز ۱ بند AF: همون الگوی
            // WorkOrder.Create/Close - کسی که سفارش/بچ تولید را ایجاد یا ویرایش کرده، نمی‌تواند
            // همان رکورد را خودش Release/Complete/Close یا از نظر کیفیت Release/Reject کند.
            new { FirstResource = "Production", FirstAction = "Create", SecondResource = "Production", SecondAction = "Approve", Description = "کاربر نمی‌تواند سفارش/بچ تولیدی را که خودش ایجاد کرده، خودش Release/تایید کیفیت کند." },
            new { FirstResource = "Production", FirstAction = "Edit", SecondResource = "Production", SecondAction = "Approve", Description = "کاربر نمی‌تواند سفارش تولیدی را که خودش برنامه‌ریزی (Plan) کرده، خودش Release/Close کند." }
        };

        foreach (var policy in defaultPolicies)
        {
            var permissionA = await GetOrCreatePermissionAsync(db, cache, policy.FirstResource, policy.FirstAction);
            var permissionB = await GetOrCreatePermissionAsync(db, cache, policy.SecondResource, policy.SecondAction);

            var exists = await db.SoDPolicies.AnyAsync(x => x.IsActive &&
                ((x.PermissionAId == permissionA.PermissionId && x.PermissionBId == permissionB.PermissionId) ||
                 (x.PermissionAId == permissionB.PermissionId && x.PermissionBId == permissionA.PermissionId)));

            if (exists)
                continue;

            db.SoDPolicies.Add(new SoDPolicy
            {
                PermissionAId = permissionA.PermissionId,
                PermissionBId = permissionB.PermissionId,
                Description = policy.Description,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedFromRolePermissionsAsync(PEMSDbContext db, Dictionary<string, Permission> cache)
    {
        var rows = await db.RolePermissions.ToListAsync();

        foreach (var row in rows)
        {
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "View", row.CanView);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Create", row.CanCreate);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Edit", row.CanEdit);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Delete", row.CanDelete);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Approve", row.CanApprove);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "ManualNumber", row.CanManualNumber);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Freeze", row.CanFreeze);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Unfreeze", row.CanUnfreeze);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "Hold", row.CanHold);
            await GrantIfTrueAsync(db, cache, row.RoleName, row.ModuleName, "ReleaseHold", row.CanReleaseHold);
        }
    }

    private static async Task SeedFromRoleModuleActionsAsync(PEMSDbContext db, Dictionary<string, Permission> cache)
    {
        var rows = await db.RoleModuleActions.ToListAsync();

        foreach (var row in rows)
            await GrantIfTrueAsync(db, cache, row.RoleName, row.Module, row.Action, true);
    }

    private static async Task GrantIfTrueAsync(
        PEMSDbContext db, Dictionary<string, Permission> cache, string roleName, string resourceName, string actionName, bool granted)
    {
        if (!granted) return;

        var permission = await GetOrCreatePermissionAsync(db, cache, resourceName, actionName);

        var roleId = await db.Roles.Where(r => r.Name == roleName).Select(r => (int?)r.Id).FirstOrDefaultAsync();
        if (roleId == null) return; // نقش هنوز در AspNetRoles وجود ندارد - چیزی برای Grant نیست

        var exists = await db.RolePermissionGrants
            .AnyAsync(g => g.RoleId == roleId.Value && g.PermissionId == permission.PermissionId);
        if (exists) return;

        db.RolePermissionGrants.Add(new RolePermissionGrant
        {
            RoleId = roleId.Value,
            PermissionId = permission.PermissionId,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Permission> GetOrCreatePermissionAsync(
        PEMSDbContext db, Dictionary<string, Permission> cache, string resourceName, string actionName)
    {
        var name = $"{resourceName}.{actionName}";
        if (cache.TryGetValue(name, out var cached)) return cached;

        var existing = await db.Permissions.FirstOrDefaultAsync(p => p.Name == name);
        if (existing != null)
        {
            cache[name] = existing;
            return existing;
        }

        var permission = new Permission
        {
            Name = name,
            ResourceName = resourceName,
            ActionName = actionName,
            IsDestructive = DestructiveActions.Contains(actionName),
            IsActive = true,
            CreatedDate = DateTime.Now
        };
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();

        cache[name] = permission;
        return permission;
    }

    private static async Task SeedRoleAssignmentsFromUserRolesAsync(PEMSDbContext db)
    {
        var userRoles = await db.UserRoles.Select(ur => new { ur.UserId, ur.RoleId }).ToListAsync();

        foreach (var ur in userRoles)
        {
            var exists = await db.RoleAssignments.AnyAsync(a =>
                a.UserId == ur.UserId && a.RoleId == ur.RoleId && a.ScopeType == "Global");
            if (exists) continue;

            db.RoleAssignments.Add(new RoleAssignment
            {
                UserId = ur.UserId,
                RoleId = ur.RoleId,
                ScopeType = "Global",
                AssignmentType = "Direct",
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }
}
