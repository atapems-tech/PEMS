using System.ComponentModel.DataAnnotations;

namespace PEMS.Web.Areas.Admin.Models;

public class RoleListItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public int UserCount { get; set; }

    public bool IsSystemRole { get; set; }

    public string? Domain { get; set; }

    public string? Category { get; set; }

    public int SortOrder { get; set; }

    // طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۹/۱۴: شمارش مجوزهای فعالِ
    // این نقش (RolePermissionGrant، منبع واقعی زمان اجرا) - نه یک وضعیت جدید (Draft/Active/...)،
    // صرفاً دید‌پذیری. صفر یعنی «هنوز پیکربندی نشده»، نه یک خطا یا حالت نامعتبر.
    public int PermissionCount { get; set; }
}


public class RoleFormViewModel
{
    [Required]
    [Display(Name = "Role Name (English, no spaces)")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Display Name")]
    public string? DisplayName { get; set; }

    public string? Description { get; set; }
}
