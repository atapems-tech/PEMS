using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۶: فقط هدف را نگه می‌دارد،
/// هرگز مقدار واقعی/Variance/درصد تحقق را - آن‌ها همیشه در لحظه‌ی گزارش‌گیری از
/// ProductionOutput محاسبه می‌شوند (طبق قاعده‌ی صریح ProductionTarget = target,
/// ProductionOutput = actual). ProductionOrderId اختیاری است - هدف می‌تواند به یک سفارش خاص
/// یا فقط به ترکیب متریال/مرکز کاری/تاریخ (بدون سفارش مشخص) وصل باشد.
/// </summary>
public class ProductionTarget : BaseEntity
{
    public int ProductionTargetId { get; set; }

    // طبق قرارداد موجود کل پروژه (DateTime در همه‌جا، بدون DateOnly) - فقط بخش تاریخ معنی‌دار
    // است؛ همیشه با Time=00:00:00 ذخیره/مقایسه می‌شود (دقیقاً مثل الگوی WorkOrder.DueDate).
    public DateTime TargetDate { get; set; }

    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }

    public int? ShiftDefinitionId { get; set; }
    public ShiftDefinition? ShiftDefinition { get; set; }

    public decimal TargetQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
