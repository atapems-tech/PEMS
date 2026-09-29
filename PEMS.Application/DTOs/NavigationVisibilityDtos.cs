namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند «PEMS Phase 6 — User Module & Navigation Visibility Architecture»: نمایش یک ردیف
/// استثنای دستیِ Visibility. عمداً بدون هیچ خاصیت مجوزی/Scope - این DTO فقط «چه چیزی دیده
/// می‌شود» را حمل می‌کند، نه «چه کاری مجاز است» (آن تصمیم همچنان فقط مال AccessControlService است).
/// </summary>
public class NavigationVisibilityOverrideDto
{
    public int NavigationVisibilityOverrideId { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string ModuleKey { get; set; } = string.Empty;
    public string? ItemKey { get; set; }
    public bool IsVisible { get; set; }
    public string? Reason { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
