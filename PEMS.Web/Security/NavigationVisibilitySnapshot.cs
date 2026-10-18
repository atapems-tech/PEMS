namespace PEMS.Web.Security;

/// <summary>
/// طبق سند «PEMS Phase 6»: نتیجه‌ی محاسبه‌شده‌ی یک‌بار-در-هر-Render برای سایدبار یک کاربر واقعی -
/// _SidebarMenu.cshtml فقط این را می‌خواند، هرگز مستقیماً NavigationVisibilityResolver یا
/// AccessControlService را صدا نمی‌زند (دقیقاً هم‌الگو با canSeeQuickSetup/canSeeImportCenter
/// موجود که یک‌بار در بالای فایل محاسبه و بارها در ادامه خوانده می‌شوند).
/// </summary>
public class NavigationVisibilitySnapshot
{
    private readonly HashSet<string> _visibleModules;
    private readonly HashSet<string> _visibleCmmsItems;

    public NavigationVisibilitySnapshot(HashSet<string> visibleModules, HashSet<string> visibleCmmsItems)
    {
        _visibleModules = visibleModules;
        _visibleCmmsItems = visibleCmmsItems;
    }

    public bool IsModuleVisible(string moduleKey) => _visibleModules.Contains(moduleKey);

    /// <summary>
    /// آیتم‌های CmmsIntegration بدون Resource نگاشته‌شده (Instrument/Asset/SparePart) و بدون
    /// Override صریح، همیشه true برمی‌گردانند - چون تنها دروازه‌ی واقعی‌شان همین سطح ماژول است
    /// (دقیقاً رفتار امروز، قبل از فاز ۶).
    /// </summary>
    public bool IsCmmsItemVisible(string itemKey) => _visibleCmmsItems.Contains(itemKey);

    /// <summary>برای سرکوب یک زیرگروه/ماژول خالی - وقتی هیچ‌کدام از آیتم‌های داده‌شده دیده نمی‌شوند.</summary>
    public bool AnyCmmsItemVisible(params string[] itemKeys) => itemKeys.Any(IsCmmsItemVisible);
}
