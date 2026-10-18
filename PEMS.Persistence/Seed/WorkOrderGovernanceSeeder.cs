using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Common;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Workflow;
using static PEMS.Persistence.Workflow.WorkOrderWorkflow;

namespace PEMS.Persistence.Seed;

/// <summary>
/// Phase 3B - پایه‌ی حاکمیت Work Order (Identity/مجوزها) طبق WORKFLOW_PLATFORM_PHASE3A_WORK_ORDER_GOVERNANCE.md.
/// یک‌بار مصرف با نشانگر «WorkOrder.GovernanceBaseline.v1» در جدول Permissions (تنظیمات دستی بعدی مدیر بازنویسی نمی‌شود):
///  * نقش‌های سازمانی هدف (Maintenance/Operations) ساخته می‌شوند و مجوزهای وظیفه‌ای می‌گیرند؛
///  * نقش‌های Engineering و MaintenanceEngineer اختیار Create/Edit/Approve/Delete گسترده‌ی Work Order را از دست می‌دهند
///    (Engineering فقط به‌عنوان دیسیپلین مبدأ: تأیید/تصویب)؛ هیچ ردیفی حذف نمی‌شود، فقط IsActive=false / پرچم‌ها False می‌شوند؛
///  * OrganizationDomain برای دپارتمان‌های هم‌نام با دامنه (Maintenance/Engineering/Operations/Warehouse) که هنوز Null هستند تنظیم می‌شود.
/// نقش‌ها فقط «طرح» هستند تا کاربران واقعی به آن‌ها اختصاص داده شوند؛ هیچ کاربری خودکار عضو نمی‌شود.
/// </summary>
public static class WorkOrderGovernanceSeeder
{
    public const string Marker = "WorkOrder.GovernanceBaseline.v1";

    public static readonly string[] MaintenanceRoles = { "MNT.Manager", "MNT.PlanningHead", "MNT.PlanningExpert", "MNT.DisciplineHead", "MNT.SeniorExpert", "MNT.Expert" };
    public const string OperationsRole = "OPS.IssueAuthority";

    // نقش → مجوزهای وظیفه‌ای (Perm) + پرچم‌های قدیمی ماژول WorkOrder (برای مسیر Legacy و ماژول‌هایی که هنوز RolePermission می‌خوانند)
    private static readonly (string Role, string[] Perms, bool Create, bool Edit, bool Approve)[] Matrix =
    {
        ("MNT.Manager",        new[] { Perm.View, Perm.Reject, Perm.CancelPreIssue, Perm.CancelExecution, Perm.FinalApprove }, false, false, true),
        ("MNT.PlanningHead",   new[] { Perm.View, Perm.Create, Perm.Plan, Perm.Prepare, Perm.Reject, Perm.CancelPreIssue }, true, false, false),
        ("MNT.PlanningExpert", new[] { Perm.View, Perm.Create, Perm.Plan, Perm.Prepare }, true, false, false),
        ("MNT.DisciplineHead", new[] { Perm.View, Perm.Create, Perm.Assign, Perm.Hold, Perm.Execute, Perm.Verify, Perm.FinalApprove }, true, true, false),
        ("MNT.SeniorExpert",   new[] { Perm.View, Perm.Execute, Perm.Verify }, false, true, false),
        ("MNT.Expert",         new[] { Perm.View, Perm.Execute }, false, true, false),
        ("OPS.IssueAuthority", new[] { Perm.View, Perm.Issue, Perm.Verify, Perm.FinalApprove }, false, false, false),
        // Engineering: فقط به‌عنوان دیسیپلین مبدأ (تأیید تکمیل / تصویب نهایی) + مشاهده
        (SystemRoles.DisciplineEngineer,   new[] { Perm.View, Perm.Verify }, false, false, false),
        (SystemRoles.EngineeringSupervisor, new[] { Perm.View, Perm.FinalApprove }, false, false, false),
        (SystemRoles.EngineeringManager,   new[] { Perm.View, Perm.FinalApprove }, false, false, false),
        // نقش قدیمی MaintenanceEngineer: فقط مشاهده + ثبت داده‌ی کاری (Labor/Task/Spare)؛ اختیار چرخه‌عمر حذف می‌شود
        (SystemRoles.MaintenanceEngineer,  new[] { Perm.View }, false, true, false),
    };

    private static readonly string[] RevokedFromExisting =
        { SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor, SystemRoles.DisciplineEngineer, SystemRoles.MaintenanceEngineer };

    public static async Task<Permission> GetOrCreatePermissionAsync(PEMSDbContext db, string name)
    {
        var existing = await db.Permissions.FirstOrDefaultAsync(p => p.Name == name);
        if (existing != null) return existing;
        var dot = name.IndexOf('.');
        var p = new Permission { Name = name, ResourceName = name[..dot], ActionName = name[(dot + 1)..], IsActive = true, CreatedDate = DateTime.Now };
        db.Permissions.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>مجوزهای وظیفه‌ای در کاتالوگ (Idempotent؛ برای Validator گردش‌کار لازم است).</summary>
    public static async Task EnsurePermissionsAsync(PEMSDbContext db)
    {
        foreach (var name in Perm.Workflow.Append(Perm.View)) await GetOrCreatePermissionAsync(db, name);
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        await EnsurePermissionsAsync(db);
        if (await db.Permissions.AnyAsync(p => p.Name == Marker)) return;

        // دپارتمان‌های هم‌نام با دامنه (فقط اگر هنوز مقدار ندارند)
        foreach (var d in await db.Departments.Where(x => x.OrganizationDomain == null).ToListAsync())
        {
            var match = OrganizationDomains.All.FirstOrDefault(o => string.Equals(o, d.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null) d.OrganizationDomain = match;
        }
        await db.SaveChangesAsync();

        // نقش‌ها
        foreach (var role in MaintenanceRoles.Append(OperationsRole))
            if (!await db.Roles.AnyAsync(r => r.Name == role))
                db.Roles.Add(new ApplicationRole { Name = role, NormalizedName = role.ToUpperInvariant() });
        await db.SaveChangesAsync();

        // پرچم‌های قدیمی + مجوزهای وظیفه‌ای
        foreach (var (role, perms, create, edit, approve) in Matrix)
        {
            var roleId = await db.Roles.Where(r => r.Name == role).Select(r => (int?)r.Id).FirstOrDefaultAsync();
            if (roleId == null) continue;

            var rp = await db.RolePermissions.FirstOrDefaultAsync(x => x.RoleName == role && x.ModuleName == "WorkOrder");
            if (rp == null) db.RolePermissions.Add(rp = new RolePermission { RoleName = role, ModuleName = "WorkOrder", CreatedDate = DateTime.Now });
            rp.CanView = true; rp.CanCreate = create; rp.CanEdit = edit; rp.CanApprove = approve; rp.CanDelete = false;

            foreach (var p in perms)
            {
                var perm = await GetOrCreatePermissionAsync(db, p);
                var grant = db.RolePermissionGrants.Local.FirstOrDefault(g => g.RoleId == roleId && g.PermissionId == perm.PermissionId)
                    ?? await db.RolePermissionGrants.FirstOrDefaultAsync(g => g.RoleId == roleId && g.PermissionId == perm.PermissionId);
                if (grant == null) db.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = roleId.Value, PermissionId = perm.PermissionId, IsActive = true, CreatedDate = DateTime.Now });
                else grant.IsActive = true;
            }
            // مجوزهای قدیمی ماژول که پرچم‌هایشان False شد: Grant غیرفعال (نه حذف)
            foreach (var (legacyAction, allowed) in new[] { ("Create", create), ("Edit", edit), ("Approve", approve), ("Delete", false) })
            {
                if (allowed) continue;
                if (!RevokedFromExisting.Contains(role)) continue;
                var legacy = await db.Permissions.FirstOrDefaultAsync(p => p.Name == "WorkOrder." + legacyAction);
                if (legacy == null) continue;
                var g = db.RolePermissionGrants.Local.FirstOrDefault(x => x.RoleId == roleId && x.PermissionId == legacy.PermissionId)
                    ?? await db.RolePermissionGrants.FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == legacy.PermissionId);
                if (g != null) g.IsActive = false;
            }
            // مجوزهای قدیمی که پرچم True دارند (Edit/Approve/Create برای نقش‌های جدید) → Grant
            foreach (var (legacyAction, allowed) in new[] { ("Create", create), ("Edit", edit), ("Approve", approve) })
            {
                if (!allowed) continue;
                var perm = await GetOrCreatePermissionAsync(db, "WorkOrder." + legacyAction);
                if (!db.RolePermissionGrants.Local.Any(g => g.RoleId == roleId && g.PermissionId == perm.PermissionId) && !await db.RolePermissionGrants.AnyAsync(g => g.RoleId == roleId && g.PermissionId == perm.PermissionId))
                    db.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = roleId.Value, PermissionId = perm.PermissionId, IsActive = true, CreatedDate = DateTime.Now });
            }
        }
        await db.SaveChangesAsync();

        db.Permissions.Add(new Permission { Name = Marker, ResourceName = "WorkOrder", ActionName = "GovernanceBaseline.v1", IsActive = true, CreatedDate = DateTime.Now });
        await db.SaveChangesAsync();
    }
}
