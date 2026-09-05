using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10e: یک رویداد تحویل شیفت بین دو نفر مشخص - عمداً یک Entity زمان‌بندی کامل Shift
/// نیست (بدون جدول اصلی Shift/برنامه‌ی کاری - طبق عدم‌هدف صریح سند). Permit ندارد به‌عنوان
/// والد - چندین Permit/WorkOrder را هم‌زمان در بر می‌گیرد (Many-to-many واقعی، نه فرزند یک
/// Permit)، پس ماژول بالادستی مستقل خودش را دارد، نه یک کارت روی Permit/Details.
/// Prepare/Acknowledge/Complete هرکدام علاوه بر گیت درشت RoleModuleAction، با شناسه‌ی دقیق
/// کارمند (OutgoingEmployeeId/IncomingEmployeeId) هم بررسی می‌شوند - نه فقط نقش.
/// </summary>
public class ShiftHandover : BaseEntity
{
    public int ShiftHandoverId { get; set; }

    public int OutgoingEmployeeId { get; set; }
    public Employee? OutgoingEmployee { get; set; }

    public int IncomingEmployeeId { get; set; }
    public Employee? IncomingEmployee { get; set; }

    // مثال: "Day Shift 06:00-18:00" - فقط توصیفی، نه یک Entity برنامه‌ریزی جدا.
    public string? ShiftLabel { get; set; }

    public DateTime HandoverDate { get; set; }

    // یادداشت‌های آزاد کارمند خروجی - فقط قبل از Prepare قابل ثبت، بعد از آن قفل می‌شود.
    public string? Notes { get; set; }

    // Draft, Prepared, Acknowledged, Completed
    public string Status { get; set; } = "Draft";

    public DateTime? PreparedDate { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public List<ShiftHandoverItem> Items { get; set; } = new();
}
