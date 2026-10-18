using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Persistence.Seed;

/// <summary>
/// Role Catalog: Idempotent و فقط-افزودنی.
///  * نقش پایه‌ی گم‌شده ساخته می‌شود؛ هرگز حذف/تغییرنام/ادغام نمی‌شود.
///  * برای نقش موجود فقط فیلدهای «خالی» متادیتا (DisplayName/Description/Domain/Category/SortOrder) پر می‌شود -
///    مقدار ویرایش‌شده‌ی مدیر هرگز بازنویسی نمی‌شود؛ IsSystemRole فقط از false به true می‌رود.
///  * مجوز پیش‌فرض و Visibility منو فقط در لحظه‌ی «ساخته‌شدنِ همین نقش» نوشته می‌شود؛ بعد از آن تنظیمات مدیر
///    حاکم است (اجرای دوباره چیزی را برنمی‌گرداند و ردیفِ حذف‌شده‌ی مدیر را زنده نمی‌کند).
///  * هیچ کاربری به نقشی اضافه نمی‌شود. نقش سفارشی مشابه فقط گزارش می‌شود.
/// باید قبل از AccessControlSeeder.SeedFromLegacyAsync اجرا شود تا RolePermission های نقش جدید به Grant همگام شوند.
/// </summary>
public static class RoleCatalogSeeder
{
    public sealed record SeedResult(List<string> CreatedRoles, List<string> BackfilledRoles, List<(string Custom, string Baseline)> PossibleDuplicates);

    public static async Task<SeedResult> SeedAsync(IServiceProvider services)
        => await SeedAsync(services.GetRequiredService<PEMSDbContext>());

    public static async Task<SeedResult> SeedAsync(PEMSDbContext db)
    {
        var created = new List<string>();
        var backfilled = new List<string>();
        var roles = await db.Roles.ToListAsync();

        foreach (var def in RoleCatalog.Definitions)
        {
            // مقایسه‌ی بدون حساسیت به حروف: AspNetRoles روی NormalizedName ایندکس یکتا دارد؛ نقش سفارشیِ «wh.manager»
            // نباید باعث INSERT تکراری و شکست Startup شود (آن نقش همان نقش پایه محسوب و فقط متادیتای خالی‌اش پر می‌شود)
            var role = roles.FirstOrDefault(r => string.Equals(r.Name, def.Name, StringComparison.OrdinalIgnoreCase)
                                                 || string.Equals(r.NormalizedName, def.Name, StringComparison.OrdinalIgnoreCase));

            if (role == null)
            {
                role = new ApplicationRole
                {
                    Name = def.Name,
                    NormalizedName = def.Name.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Domain = def.Domain,
                    Category = def.Category,
                    IsSystemRole = true,
                    SortOrder = def.SortOrder
                };
                db.Roles.Add(role);
                // نقش + ردیف‌های ماتریس مجوزش در «یک» SaveChanges (اتمیک): قطع فرآیند وسط کار نقشِ بدون مجوزِ
                // همیشگی نمی‌سازد. ردیف‌های ماتریس با RoleName کلید می‌خورند، پس به Id نقش نیاز ندارند.
                AddDefaultPermissionRows(db, def);
                await db.SaveChangesAsync();
                roles.Add(role);
                created.Add(def.Name);

                await SeedNavigationForNewRoleAsync(db, role, def);
                continue;
            }

            var changed = false;
            if (string.IsNullOrWhiteSpace(role.DisplayName)) { role.DisplayName = def.DisplayName; changed = true; }
            if (string.IsNullOrWhiteSpace(role.Description)) { role.Description = def.Description; changed = true; }
            if (string.IsNullOrWhiteSpace(role.Domain)) { role.Domain = def.Domain; changed = true; }
            if (string.IsNullOrWhiteSpace(role.Category)) { role.Category = def.Category; changed = true; }
            if (role.SortOrder == 0) { role.SortOrder = def.SortOrder; changed = true; }
            if (!role.IsSystemRole) { role.IsSystemRole = true; changed = true; }

            if (changed) backfilled.Add(def.Name);
        }

        if (backfilled.Count > 0) await db.SaveChangesAsync();

        var duplicates = RoleCatalog.FindPossibleDuplicates(roles.Select(r => (r.Name ?? "", r.DisplayName)));
        return new SeedResult(created, backfilled, duplicates);
    }

    private static void AddDefaultPermissionRows(PEMSDbContext db, RoleCatalog.RoleDefinition def)
    {
        if (def.DefaultPermissions == null) return;

        foreach (var g in def.DefaultPermissions)
        {
            // ردیف بازمانده با همین (RoleName, Module) (مثلاً از نقشِ قبلاً حذف‌شده) بازنویسی نمی‌شود
            if (db.RolePermissions.Any(x => x.RoleName == def.Name && x.ModuleName == g.Module)) continue;

            db.RolePermissions.Add(new RolePermission
            {
                RoleName = def.Name,
                ModuleName = g.Module,
                CanView = g.View,
                CanCreate = g.Create,
                CanEdit = g.Edit,
                CanApprove = g.Approve,
                CanDelete = false,
                CreatedDate = DateTime.Now
            });
        }
    }

    private static async Task SeedNavigationForNewRoleAsync(PEMSDbContext db, ApplicationRole role, RoleCatalog.RoleDefinition def)
    {
        if (def.VisibleModules != null)
        {
            foreach (var module in def.VisibleModules)
            {
                if (await db.NavigationVisibilityOverrides.AnyAsync(o => o.RoleId == role.Id && o.ModuleKey == module && o.ItemKey == null)) continue;

                db.NavigationVisibilityOverrides.Add(new NavigationVisibilityOverride
                {
                    RoleId = role.Id,
                    ModuleKey = module,
                    ItemKey = null,
                    IsVisible = true,
                    Reason = "Role Catalog default: module required by this role's responsibilities (see docs/audits/role-catalog-audit.md)",
                    CreatedDate = DateTime.Now
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
