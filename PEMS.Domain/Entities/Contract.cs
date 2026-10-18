using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class Contract : BaseEntity
{
    [Key]
    public int ContractId { get; set; }

    public string ContractNo { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string? ScopeOfWork { get; set; }

    // ارزش اصلی/امضاشده‌ی قرارداد - پس از خروج از Draft دیگر مستقیم بازنویسی نمی‌شود؛
    // تغییرات فقط از طریق یک ContractRevision تاییدشده روی CurrentValue اعمال می‌شوند.
    public decimal? ContractValue { get; set; }

    // ارزش جاری قرارداد پس از اعمال Variation/Change Order های تاییدشده. تا زمانی‌که هیچ
    // ContractRevision تاییدی ثبت نشده، برابر ContractValue است.
    public decimal? CurrentValue { get; set; }

    public string? Currency { get; set; } = "USD";

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    // طبق ممیزی Package Procurement Contract Finalization (§C, §D، قاعده‌ی کسب‌وکار ۹):
    // پیش از این Draft/Approved/Signed/Effective در یک رشته‌ی آزاد ادغام شده بودند. مجموعه‌ی
    // مقادیر مجاز اکنون توسط ContractWorkflowService/IWorkflowRuntime کنترل می‌شود، نه ورودی آزاد.
    // Draft, UnderReview, Approved, Signed, Effective, Completed, Terminated, Rejected, Cancelled
    // داده‌های قدیمی با Status="Active" به‌عنوان میراث پیش از این معماری حفظ می‌شوند (بدون Migration مخرب)
    // و فقط قراردادهای تازه از این نقطه به بعد وارد گردش‌کار جدید می‌شوند.
    public string Status { get; set; } = "Draft";

    public DateTime? ApprovedDate { get; set; }
    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }

    public DateTime? SignedDate { get; set; }
    public int? SignedById { get; set; }
    public Employee? SignedBy { get; set; }

    public DateTime? EffectiveDate { get; set; }

    // طبق §I قاعده‌ی ۸/۹: برای ماتریس تایید - همان واژگان EquipmentCriticality (A/B/C)، مستقل از
    // ProcurementPackage.CriticalityClass چون قرارداد می‌تواند بدون خاستگاه Tender/Package هم ساخته شود
    public string? CriticalityClass { get; set; }

    // ==============================================================================
    // طبق گزارش نهایی‌سازی سخت‌افزاری Package Procurement (بند Gap ۱): کامنت اصلیِ Status از
    // قبل از این فاز (پیش از هر تغییری در این جلسه) Completed/Terminated را جزو واژگان مجاز
    // اعلام کرده بود - این یک تصمیم کسب‌وکاری اختراعی نیست، شواهد موجود در خودِ کد است. Effective
    // نقطه‌ی پایانِ گردش‌کار تایید/امضا است، نه پایانِ کاملِ عمر قرارداد.
    // ==============================================================================
    // آیا تکمیل قرارداد (Effective→Completed) نیازمند کامل‌بودن کنترل‌های بازرسی است - هم‌الگو با
    // ProcurementPackage.RequiresInspection
    public bool RequiresInspection { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime? TerminatedDate { get; set; }
    public string? TerminationReason { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    // طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability) - سمت معکوس EngineeringDocument.ContractId
    public ICollection<EngineeringDocument> EngineeringDocuments { get; set; } = new List<EngineeringDocument>();

    public ICollection<ContractRevision> Revisions { get; set; } = new List<ContractRevision>();

    public ICollection<ContractDisciplineReview> DisciplineReviews { get; set; } = new List<ContractDisciplineReview>();
}
