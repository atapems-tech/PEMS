namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند «PEMS Phase 6 — User Module & Navigation Visibility Architecture»: Visibility یک
/// لایه‌ی کاملاً جدا از Authorization است (مالک Permission: AccessControlService، مالک Scope: ABAC
/// موجود، مالک Visibility: همین Entity) - این جدول فقط استثنای دستیِ ادمین را ذخیره می‌کند، نه
/// خودِ منبع مجوز را. عمداً سبک و additive: صفر ردیف = صفر تغییر رفتار (Fallback به پیش‌فرض
/// مجوزمحور و در نهایت MenuAccess موجود). دقیقاً هم‌الگو با RolePermissionGrant
/// (RoleId ساده به‌جای Navigation Property به ApplicationRole - پیوند واقعی در EF Configuration).
///
/// «Hidden ≠ Unauthorized»: این جدول هرگز مصرف‌کننده‌ی تصمیم Authorize/Forbid نیست - فقط سایدبار
/// را می‌خواند. مخفی‌کردن یک آیتم این‌جا هرگز جایگزین [Authorize]/AccessControlService روی
/// Controller/Action واقعی‌اش نمی‌شود.
/// </summary>
public class NavigationVisibilityOverride
{
    public int NavigationVisibilityOverrideId { get; set; }

    // FK به ApplicationRole.Id (AspNetRoles) - پیوند واقعی در EF Configuration (لایه‌ی Persistence)
    public int RoleId { get; set; }

    // کلید پایدار ماژول (مثلاً "CmmsIntegration") - از NavigationCatalog، نه رشته‌ی آزاد
    public string ModuleKey { get; set; } = string.Empty;

    // در سطح ماژول Null است؛ برای استثنای یک آیتم مشخص درون ماژول (مثلاً "CmmsIntegration.WorkOrder") پر می‌شود
    public string? ItemKey { get; set; }

    public bool IsVisible { get; set; }

    public string? Reason { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
