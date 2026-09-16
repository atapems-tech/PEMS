using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش U/W: «چه چیزی باید تولید شود».
/// محصول از MaterialItem موجود گرفته می‌شود (بدون Product جدید - طبق ممیزی، MaterialItem همان
/// Product/Material Master واقعی است). یک ProductionOrder می‌تواند چند ProductionRun داشته باشد.
///
/// Status: Draft, Planned, Released, InProgress, OnHold, Completed, Closed, Cancelled - طبق
/// قرارداد رشته‌ای موجود پروژه (WorkOrder.Status/Priority هم رشته‌اند نه Enum).
/// گذار به InProgress فقط توسط ProductionRunService (وقتی اولین Run واقعاً Start می‌شود) انجام
/// می‌شود - نه یک اکشن مستقیم کاربر - تا هیچ‌وقت سفارشی «در حال اجرا» نشان داده نشود که هیچ
/// Run واقعی‌ای زیرش نیست.
/// </summary>
public class ProductionOrder : BaseEntity
{
    public int ProductionOrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal PlannedQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }

    public string Status { get; set; } = "Draft";

    // طبق «Hold/Resume»: وضعیت قبل از تعلیق، تا Resume دقیقاً به همان‌جا برگردد (نه همیشه InProgress)
    public string? PreHoldStatus { get; set; }

    public int ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }

    // اختیاری - اگر خالی باشد، تجهیز اصلیِ ProductionWorkCenter استفاده می‌شود
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? ResponsibleEmployeeId { get; set; }
    public Employee? ResponsibleEmployee { get; set; }

    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public string? HoldReason { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ProductionRun> ProductionRuns { get; set; } = new List<ProductionRun>();
}
