using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش U/Z: هویت پایدار داخلی برای
/// بچ/لات تولیدشده - نه فقط یک رشته‌ی آزاد. Quantity یک مجموع نگهداری‌شده است (مثل
/// MaterialStock.OnHandQuantity) که فقط داخل ProductionRunService، هم‌زمان با ثبت هر
/// ProductionOutput، به‌روزرسانی می‌شود.
///
/// QualityStatus طبق بند «Quality Gate» سند معماری: Produced -> AwaitingQC -> Released یا
/// Blocked/Rejected. این‌جا فقط Placeholder سطح دامنه است (بدون موتور Workflow چندنقشی) -
/// دقیقاً هم‌تراز با روندِ اصلی خودِ WorkOrder.Status که هم مستقیم توسط WorkOrderLifecycleService
/// مدیریت می‌شود، نه IWorkflowRuntime (که فقط برای زیرفرایند بازبینی چند-دیسیپلینه‌ی WorkOrder
/// استفاده شده). اگر در فازهای بعد یک زنجیره‌ی تایید چندنقشی واقعی لازم شد، IWorkflowRuntime
/// همان نقطه‌ی توسعه است - بدون نیاز به تغییر شکل این Entity.
///
/// شناسه‌ی بارکد/QR این بچ به‌صورت مستقیم روی این Entity نگه داشته نمی‌شود - طبق الگوی موجود
/// Equipment/MaterialItem (که آن‌ها هم فیلد BarcodeIdentityId ندارند)، از طریق
/// IBarcodeService.GetActiveByTargetAsync("ProductionBatch", ProductionBatchId) به‌صورت معکوس
/// Resolve می‌شود.
/// </summary>
public class ProductionBatch : BaseEntity
{
    public int ProductionBatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    // مجموع نگهداری‌شده - فقط توسط ProductionRunService.RecordOutputAsync تغییر می‌کند
    public decimal Quantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public DateTime ProducedDate { get; set; } = DateTime.Now;

    // Produced, AwaitingQC, Released, Blocked, Rejected
    public string QualityStatus { get; set; } = "Produced";

    public string? QualityStatusReason { get; set; }
    public DateTime? QualityStatusChangedDate { get; set; }
    public int? QualityStatusChangedById { get; set; }
    public Employee? QualityStatusChangedBy { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();

    // طبق بخش AA (Traceability Lifecycle در سند معماری): قلاب ژنالوژی برای فاز ۳+ -
    // ورودی‌هایی که این بچ به‌عنوان مادهٔ منبع مصرف کرده‌اند (Batch Genealogy). فاز ۱ فقط
    // این رابطه را برقرار می‌کند - موتور ژنالوژی کامل ساخته نمی‌شود.
    public ICollection<ProductionInput> ConsumedAsSourceIn { get; set; } = new List<ProductionInput>();
}
