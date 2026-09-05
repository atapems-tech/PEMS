using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10e: یک ردیف Snapshot - فقط یک‌بار در لحظه‌ی Prepare نوشته می‌شود، هرگز دوباره
/// تولید نمی‌شود (یک تحویل شیفت باید آنچه در لحظه‌ی تحویل درست بوده را نشان دهد، نه یک Query
/// زنده که بعداً تغییر می‌کند - همون اصل انجماد GasReading.Result در CMMS-10d). یک ستون
/// تفکیک‌کننده‌ی ItemType به‌جای ۸ جدول فرزند جدا - دقیقاً همون الگوی GasReading.Parameter.
/// </summary>
public class ShiftHandoverItem : BaseEntity
{
    public int ShiftHandoverItemId { get; set; }

    public int ShiftHandoverId { get; set; }
    public ShiftHandover? ShiftHandover { get; set; }

    // ActivePermit, SuspendedPermit, OpenWorkOrder, ActiveIsolation, ValidGasTest,
    // OutstandingHazard, OpenApproval, OutstandingAction
    public string ItemType { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
