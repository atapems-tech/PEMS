using PEMS.WorkflowPlatform.Dtos;

namespace PEMS.WorkflowPlatform.Interfaces;

/// <summary>
/// تنها نقطه‌ی ورودی عمومی موتور گردش‌کار. هیچ ماژول کسب‌وکاری (Engineering Document, VDR,
/// Letter, ...) نباید مستقیم به Entity های Workflow دسترسی داشته باشد - همه چیز از طریق
/// این اینترفیس. طبق سند Baseline: "No module is allowed to implement its own approval engine."
/// </summary>
public interface IWorkflowRuntime
{
    /// <summary>
    /// یک Instance جدید برای یک رکورد کسب‌وکاری شروع می‌کند و آن را به اولین مرحله می‌فرستد.
    ///
    /// firstStageParticipantRoles: اگر اولین مرحله‌ی گردش‌کار Parallel با اعضای پویا باشد
    /// (مثلاً DisciplineReview در VDR - که کدام دیسیپلین‌ها باید بازبینی کنند را خودِ ثبت‌کننده
    /// تشخیص می‌دهد)، این پارامتر آن اعضا را می‌دهد. اگر null/خالی باشد، از تنظیمات ایستای
    /// مرحله استفاده می‌شود (رفتار پیش‌فرض)، و اگر مرحله‌ی اول ذاتاً اعضای پویا نداشته باشد
    /// (تعریف با ResponsibleRole ثابت)، این پارامتر نادیده گرفته می‌شود.
    /// </summary>
    Task<int> StartInstanceAsync(
        string module,
        string businessObjectType,
        int businessObjectId,
        string workflowCategory,
        int? projectId,
        int? initiatedByEmployeeId,
        IReadOnlyList<string>? firstStageParticipantRoles = null);

    /// <summary>
    /// یک اکشن (Approve/Reject/Return/Skip/...) را روی Instance اجرا می‌کند: مجوز نقش را
    /// چک می‌کند، رکورد InstanceStage مربوطه را می‌بندد، اگر مرحله کامل شده باشد گذار بعدی
    /// را طبق WorkflowTransition پیدا می‌کند، و یک رکورد WorkflowHistory تغییرناپذیر ثبت می‌کند.
    ///
    /// nextStageParticipantRoles: برای مراحل Parallel که اعضایشان از قبل در تعریف گردش‌کار
    /// ثابت نیست (مثلاً IDC - که کدام دیسیپلین‌ها باید بازبینی کنند را خودِ سازنده‌ی مدرک
    /// تشخیص می‌دهد، نه یک نقش سازمانی ثابت)، این پارامتر لیست دقیق اعضای مرحله‌ی بعدی را
    /// می‌دهد و جایگزین تنظیمات ایستای WorkflowStage.ResponsibleRole می‌شود. اگر null/خالی
    /// باشد، از تنظیمات ایستای خود مرحله استفاده می‌شود (رفتار پیش‌فرض).
    /// </summary>
    Task ExecuteActionAsync(
        int workflowInstanceId,
        string actionType,
        int? actorEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? comment,
        string? ipAddress,
        string? userAgent,
        IReadOnlyList<string>? nextStageParticipantRoles = null);

    Task<WorkflowInstanceDto?> GetInstanceAsync(string businessObjectType, int businessObjectId);

    Task<WorkflowInstanceDto?> GetInstanceByIdAsync(int workflowInstanceId);

    Task<IReadOnlyList<WorkflowHistoryDto>> GetHistoryAsync(int workflowInstanceId);

    Task<IReadOnlyList<PendingTaskDto>> GetPendingTasksAsync(int employeeId, IReadOnlyList<string> actorRoles);
}
