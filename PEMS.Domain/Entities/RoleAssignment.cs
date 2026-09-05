using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۱۱-۱۳: تخصیص یک Role به یک کاربر مشخص،
/// همراه با Scope و بازه‌ی زمانی - این‌جا نگه‌داری می‌شود، نه در AspNetUserRoles استاندارد.
/// دلیل عمدی: AspNetUserRoles در لحظه‌ی ورود در Cookie (۸ ساعت Sliding) پخته می‌شود و هیچ
/// اعتبارسنجی زنده‌ای در هر Request ندارد - اگر Scope/انقضا روی همون جدول اضافه می‌شد، هر
/// چک قدیمی ([Authorize(Roles=)]، آرایه‌های نقش دستی، MenuAccess) بی‌صدا آن محدودیت را نادیده
/// می‌گرفت. AccessControlService این جدول را زنده/به‌ازای هر Request می‌خواند، نه از Cookie.
/// در فاز ۱، هر ردیف AspNetUserRoles موجود یک RoleAssignment(ScopeType=Global, بدون انقضا)
/// معادل می‌گیرد (AccessControlSeeder) - یعنی سیستم جدید از روز اول همون دسترسی واقعی امروز را می‌بیند.
/// </summary>
public class RoleAssignment : BaseEntity
{
    public int RoleAssignmentId { get; set; }

    // FK به ApplicationUser.Id (AspNetUsers)
    public int UserId { get; set; }

    // FK به ApplicationRole.Id (AspNetRoles)
    public int RoleId { get; set; }

    // Global, Department, Discipline, Project, Plant, Area, Warehouse, Shift, Document, RecordOwner
    public string ScopeType { get; set; } = "Global";

    // مقدار Scope (مثلاً شناسه‌ی Discipline/Department/Project) - Global یعنی بدون محدودیت
    public string? ScopeValue { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    // Direct, Delegated, Emergency
    public string AssignmentType { get; set; } = "Direct";

    public int? DelegatedByUserId { get; set; }

    public string? Reason { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
