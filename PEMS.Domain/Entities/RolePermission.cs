using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// Permission Matrix طبق سند Security Requirements: مجوزهای ریزدانه به‌ازای هر Role و هر ماژول،
/// جدای نقش‌های کلی (SystemAdmin, EngineeringManager, ...). داده‌محور است تا بدون نیاز به
/// Deploy کد جدید، مدیر سیستم بتواند دسترسی‌ها را از UI تنظیم کند.
/// </summary>
public class RolePermission
{
    [Key]
    public int RolePermissionId { get; set; }

    // نام Role (باید با نام‌های تعریف‌شده در SystemRoles یکی باشد)
    public string RoleName { get; set; } = string.Empty;

    // نام ماژول، مثلاً: VDR, EngineeringDocuments, Contracts, MaterialRequisition, Equipment
    public string ModuleName { get; set; } = string.Empty;

    public bool CanView { get; set; } = true;
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }

    // آیا این نقش اجازه دارد شماره‌ی دستی مدرک را جایگزین شماره‌ی خودکار کند
    public bool CanManualNumber { get; set; }

    // آیا این نقش اجازه دارد مدرک را منجمد/رفع انجماد کند - طبق Phase E-01
    public bool CanFreeze { get; set; }
    public bool CanUnfreeze { get; set; }

    // طبق بخش M (Hold Lifecycle) ممیزی: دقیقاً هم‌الگو با CanFreeze/CanUnfreeze - Hold یک
    // عملیات مستقل از Freeze است (نه رونویسی همان دو ستون)
    public bool CanHold { get; set; }
    public bool CanReleaseHold { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
