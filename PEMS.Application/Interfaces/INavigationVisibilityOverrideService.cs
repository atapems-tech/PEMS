using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند «PEMS Phase 6»: دسترسی داده‌ای به NavigationVisibilityOverride - فقط CRUD روی
/// استثنای دستیِ Visibility، هیچ تصمیم مجوزی این‌جا گرفته نمی‌شود (آن مسئولیت همچنان فقط مال
/// IAccessControlService است؛ این سرویس هرگز آن را صدا نمی‌زند و هرگز جایگزینش نمی‌شود).
/// </summary>
public interface INavigationVisibilityOverrideService
{
    /// <summary>همه‌ی استثناهای فعال برای مجموعه‌ای از نقش‌ها (با شناسه) - برای صفحه‌ی مدیریت که RoleId می‌داند.</summary>
    Task<List<NavigationVisibilityOverrideDto>> GetOverridesForRolesAsync(IEnumerable<int> roleIds);

    /// <summary>
    /// همه‌ی استثناهای فعال برای مجموعه‌ای از نام نقش - برای محاسبه‌ی سایدبار یک کاربر واقعی که
    /// فقط IReadOnlyList&lt;string&gt; Roles (از ICurrentUserService) در اختیار دارد، نه RoleId.
    /// </summary>
    Task<List<NavigationVisibilityOverrideDto>> GetOverridesForRoleNamesAsync(IEnumerable<string> roleNames);

    /// <summary>همه‌ی استثناهای یک نقش مشخص - برای صفحه‌ی مدیریت Visibility همان نقش.</summary>
    Task<List<NavigationVisibilityOverrideDto>> GetOverridesForRoleAsync(int roleId);

    /// <summary>ثبت/به‌روزرسانی یک استثنا (سطح ماژول اگر itemKey تهی باشد، وگرنه سطح آیتم).</summary>
    Task SetOverrideAsync(int roleId, string moduleKey, string? itemKey, bool isVisible, string? reason, string actingUser);

    /// <summary>حذف یک استثنا - بازگشت به رفتار پیش‌فرض (MenuAccess یا مجوزمحور).</summary>
    Task ClearOverrideAsync(int roleId, string moduleKey, string? itemKey);
}
