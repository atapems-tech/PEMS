using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Help System — Phase 3» بخش ۸ («Workflow Graph Adapter»): این سرویس هرگز یک
/// نسخه‌ی دستیِ گراف گردش‌کار را نگه نمی‌دارد - مستقیماً از جداول واقعیِ موتور گردش‌کار
/// (WorkflowStage/WorkflowTransition، آخرین نسخه‌ی Published) می‌خواند، پس هیچ‌وقت نمی‌تواند
/// از رفتار واقعی سیستم عقب بیفتد یا با آن ناسازگار شود.
/// </summary>
public interface IHelpWorkflowDiagramService
{
    /// <summary>
    /// module/category دقیقاً همان مقادیر WorkflowDefinition.Module/Category هستند (مثلاً
    /// "EngineeringDocument"/"Approval"). اگر چنین گردش‌کاری وجود نداشته باشد، null برمی‌گردد -
    /// هرگز یک نمودار جعلی نمی‌سازد.
    /// </summary>
    Task<HelpVisualRenderDto?> BuildAsync(string module, string category, string languageCode);

    /// <summary>
    /// طبق بخش ۱۳ سند (مدیریت ادمین): فهرست واقعیِ گردش‌کارهای موجود در موتور - برای Dropdown
    /// ادمین، تا نتواند Module/Category ساختگی وارد کند.
    /// </summary>
    Task<IReadOnlyList<(string Module, string Category, string Name)>> GetAvailableWorkflowSourcesAsync();
}
