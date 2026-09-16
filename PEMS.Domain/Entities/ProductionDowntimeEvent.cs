using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۷: رویداد واقعی توقف تولید -
/// جایگزین WorkOrder.DowntimeHours/FailureRecord.DowntimeHours نمی‌شود (آن دو فیلد دست‌نخورده
/// می‌مانند) بلکه یک رویداد قابل‌ممیزی با شروع/پایان واقعی اضافه می‌کند. EquipmentId عمداً
/// اختیاری است - توقف عملیاتی همیشه به خرابی تجهیز نگاشت نمی‌شود (مثلاً کمبود ماده‌ی اولیه یا
/// تغییر محصول). DurationMinutes ذخیره نمی‌شود - همیشه از StartTime/EndTime مشتق می‌شود تا هیچ
/// ناسازگاری بین دو مقدار مستقل رخ ندهد.
/// </summary>
public class ProductionDowntimeEvent : BaseEntity
{
    public int ProductionDowntimeEventId { get; set; }

    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    public int ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public DateTime StartTime { get; set; }

    // Null یعنی توقف هنوز باز/جاری است
    public DateTime? EndTime { get; set; }

    // Equipment, Process, Material, Utility, Planned, Other - فهرست کنترل‌شده
    // (ProductionDowntimeService.CauseCategories اعتبارسنجی می‌کند)
    public string CauseCategory { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? SourceWorkRequestId { get; set; }
    public WorkRequest? SourceWorkRequest { get; set; }

    public int? SourceWorkOrderId { get; set; }
    public WorkOrder? SourceWorkOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
