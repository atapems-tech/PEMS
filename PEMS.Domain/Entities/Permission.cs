namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۶: مجوز ریزدانه به‌شکل «Resource.Action»
/// (مثلاً EngineeringDocuments.View) - نقطه‌ی مرجع مرکزی برای IAccessControlService.
/// جایگزین RolePermission/RoleModuleAction نیست (آن دو دست‌نخورده باقی می‌مانند و منبع Seed
/// این جدول هستند)؛ لایه‌ی عمومی‌شده‌ی زیر هر دو است.
/// </summary>
public class Permission
{
    public int PermissionId { get; set; }

    // "{ResourceName}.{ActionName}" - یکتا
    public string Name { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ActionName { get; set; } = string.Empty;

    // طبق بند ۷ سند: Delete/HardDelete/Cancel/Reject/Approve/Issue/Unissue/Close/Reopen/
    // Freeze/Unfreeze/Override/EmergencyOverride - باید همیشه صریح باشند، هیچ‌وقت ضمنی
    public bool IsDestructive { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
