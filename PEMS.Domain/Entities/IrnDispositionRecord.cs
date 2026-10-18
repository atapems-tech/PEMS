using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق ممیزی Material Management (Gap B) + Step 3: یک رکورد Disposition برای یک IrnReceipt -
/// تصمیم فنی بازرس (Accepted/Rejected/Hold به‌ازای هر خط، روی IrnReceiptLine ثبت می‌شود) + تایید
/// رسمی (از طریق IWorkflowRuntime موجود - همون الگوی NcrApprovalWorkflowSeeder، بدون موتور
/// Workflow جدید). اگر تایید Reject شود، بازرس باید Disposition را دوباره ثبت کند - پس ممکن است
/// چند IrnDispositionRecord برای یک IrnReceipt وجود داشته باشد (آخرین رکورد با WorkflowOutcome
/// هنوز null یا "Approved"، معتبر است). هیچ FK مستقیم به WorkflowInstance ذخیره نمی‌شود - دقیقاً
/// مثل الگوی NCR، هر بار از طریق IWorkflowRuntime.GetInstanceAsync(BusinessObjectType, Id) واکشی
/// می‌شود.
/// </summary>
public class IrnDispositionRecord : BaseEntity
{
    public int IrnDispositionRecordId { get; set; }

    public int IrnReceiptId { get; set; }
    public IrnReceipt? IrnReceipt { get; set; }

    public int DispositionedById { get; set; }
    public Employee? DispositionedBy { get; set; }

    public DateTime DispositionedDate { get; set; } = DateTime.Now;

    // Accepted, PartiallyAccepted, Rejected, Hold - از روی مجموع خطوط IrnReceiptLine محاسبه و
    // این‌جا ذخیره می‌شود (برای نمایش سریع، بدون نیاز به بازمحاسبه هر بار).
    public string OverallResult { get; set; } = string.Empty;

    public string? Notes { get; set; }

    // اختیاری - طبق دستور صریح Step 3 بخش ۱۵: NCR به‌صورت خودکار برای هر Reject ساخته نمی‌شود؛
    // فقط اگر بازرس صریحاً یکی از طریق فرآیند موجود QCController.NcrCreate بسازد و اینجا وصل کند.
    public int? NonConformanceReportId { get; set; }
    public NonConformanceReport? NonConformanceReport { get; set; }

    // Pending (منتظر تایید Workflow), Approved, Rejected (تایید رد شد - بازرس باید دوباره ثبت کند)
    public string WorkflowOutcome { get; set; } = "Pending";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
