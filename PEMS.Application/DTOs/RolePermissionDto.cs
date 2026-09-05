namespace PEMS.Application.DTOs;

public class RolePermissionDto
{
    public int RolePermissionId { get; set; }
    public string RoleName { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanManualNumber { get; set; }
    public bool CanFreeze { get; set; }
    public bool CanUnfreeze { get; set; }
    public bool CanHold { get; set; }
    public bool CanReleaseHold { get; set; }
}

/// <summary>
/// ورودی یک ردیف ماژول برای ذخیره‌ی گروهی مجوزهای یک نقش - طبق ممیزی UI/UX، به‌جای
/// یک Save جدا برای هر خانه‌ی جدول، کل ماژول‌های یک نقش با یک POST ذخیره می‌شوند.
/// </summary>
public class ModulePermissionInput
{
    public string ModuleName { get; set; } = "";
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanManualNumber { get; set; }
    public bool CanFreeze { get; set; }
    public bool CanUnfreeze { get; set; }
    public bool CanHold { get; set; }
    public bool CanReleaseHold { get; set; }
}
