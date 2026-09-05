using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10a Execution Baseline: رکورد اصلی Permit to Work. این فاز فقط Core Lifecycle
/// را پوشش می‌دهد - Approval Matrix واقعی (CMMS-10b)، LOTO (CMMS-10c)، Gas Test (CMMS-10d)،
/// SIMOPS/Shift Handover (CMMS-10e)، و Execution Gate/Emergency (CMMS-10f) در فازهای بعدی
/// اضافه می‌شوند. عمداً هیچ فیلدی برای آن‌ها این‌جا حدس زده نشده.
/// </summary>
public class Permit : BaseEntity
{
    public int PermitId { get; set; }

    // مثال: PTW-2026-0001
    public string PermitNumber { get; set; } = string.Empty;

    // Cold Work, Hot Work, Confined Space, Electrical, Excavation, Working at Height,
    // Radiography, Line Breaking, Other - رشته‌ی ساده + اعتبارسنجی سمت سرور، همون الگوی
    // WorkOrderType/Priority در این پروژه؛ نه Enum و نه جدول Lookup جدید.
    public string PermitType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // هرکدام اختیاری - دقیقاً همون الگوی WorkRequest (فقط تجهیز، فقط Functional Location،
    // یا هیچ‌کدام)
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? FunctionalLocationId { get; set; }
    public FunctionalLocation? FunctionalLocation { get; set; }

    // طبق بند ۸ سند: WorkOrder 1 ─── N Permit - FK این‌جاست، نه روی WorkOrder (همون الگوی
    // FailureRecord/MaintenanceHistory/WorkOrderLabor). Permit می‌تواند بدون WorkOrder هم باشد.
    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int RequestedByEmployeeId { get; set; }
    public Employee? RequestedByEmployee { get; set; }

    // فقط در لحظه‌ی Issue پر می‌شود
    public int? PermitIssuerEmployeeId { get; set; }
    public Employee? PermitIssuerEmployee { get; set; }

    // می‌تواند از ابتدا (Contractor Receiver) یا بعداً تعیین شود
    public int? PermitReceiverEmployeeId { get; set; }
    public Employee? PermitReceiverEmployee { get; set; }

    // Draft, Submitted, UnderReview, Approved, Issued, Active, Suspended, Closed,
    // Cancelled, Rejected, Revoked
    public string Status { get; set; } = "Draft";

    // Low, Medium, High, Critical - همون مقادیر WorkOrder.Priority
    public string Priority { get; set; } = "Medium";

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    public DateTime? ClosedDate { get; set; }

    public string? CancellationReason { get; set; }
    public string? RevocationReason { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
