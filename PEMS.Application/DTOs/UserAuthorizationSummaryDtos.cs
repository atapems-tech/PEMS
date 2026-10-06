namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS — Enterprise User-Centric Authorization Workspace Consolidation»: این DTO عیناً همان
/// شکلی است که قبلاً EffectivePermissionRowVm (خصوصی AuthorizationController) داشت - فقط به لایه‌ی
/// Application منتقل شده تا هم Admin/Authorization و هم Admin/Users بتوانند از همان یک منبع بخوانند،
/// بدون این‌که Persistence به لایه‌ی Web وابسته شود.
/// </summary>
public class EffectivePermissionDto
{
    public string ResourceName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string PermissionName => $"{ResourceName}.{ActionName}";
    public bool Allowed { get; set; }
    public bool IsDestructive { get; set; }
    public string? Detail { get; set; }
}

/// <summary>
/// طبق ممیزی این فاز: SoD در PEMS هیچ‌گاه یک وضعیت ایستای «این کاربر Conflict دارد/ندارد» را
/// محاسبه نکرده - فقط در لحظه‌ی یک تصمیم واقعی، روی یک resourceId مشخص، از روی تاریخچه‌ی
/// AuthorizationAuditLog بررسی می‌شود (AccessControlService.DecideForUserAsync). این DTO عمداً
/// «قرارگرفتن در معرض» (Exposure) نام دارد، نه «Conflict» - یعنی نقش‌های کاربر هر دو طرف یک
/// سیاست فعال را همزمان دارند؛ این هرگز به معنای رخ‌دادن واقعی یک تخلف نیست (آن فقط در لحظه‌ی
/// یک اقدام واقعی روی یک رکورد مشخص قابل‌تشخیص است) - UI هرگز نباید این را به‌عنوان «Clear/Violation»
/// قطعی نشان دهد.
/// </summary>
public class SoDExposureDto
{
    public string PermissionAName { get; set; } = string.Empty;
    public string PermissionBName { get; set; } = string.Empty;
    public string? PolicyDescription { get; set; }
    public bool UserHoldsBoth { get; set; }
}
