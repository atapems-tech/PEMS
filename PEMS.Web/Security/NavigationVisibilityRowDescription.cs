namespace PEMS.Web.Security;

/// <summary>
/// طبق سند «PEMS Phase 6»: یک ردیف تشخیصی برای صفحه‌ی مدیریت Visibility - Default/Override/
/// Effective را جدا نشان می‌دهد تا مدیر بفهمد چرا یک آیتم نمایان/مخفی است، نه فقط نتیجه‌ی نهایی
/// (دقیقاً روحیه‌ی «Effective Permissions» فاز ۴، برای Visibility به‌جای Permission).
/// </summary>
public class NavigationVisibilityRowDescription
{
    public string ModuleKey { get; set; } = string.Empty;
    public string? ItemKey { get; set; }
    public bool DefaultVisible { get; set; }
    public bool? OverrideVisible { get; set; }
    public bool EffectiveVisible { get; set; }
    public string DefaultSource { get; set; } = string.Empty;
    public bool HasOverride => OverrideVisible.HasValue;
}
