using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10d: رکورد یک تست گاز واقعی - همیشه زیرمجموعه‌ی یک Permit است، دقیقاً همون الگوی
/// IsolationPoint در CMMS-10c (Permit ├── Isolation Points └── Gas Tests، دو فرزند موازی).
/// اتمی است: در یک submit، همه‌ی GasReading ها با هم ثبت و ارزیابی می‌شوند - وضعیت Draft
/// وجود ندارد (نیازی به آن در عمل واقعی گاز-تست نیست، طبق تصمیم پلن).
/// Status هرگز پس از ثبت Edit نمی‌شود - فقط Invalidate (اصلاح = رکورد جدید + Invalidate قدیمی).
/// </summary>
public class GasTest : BaseEntity
{
    public int GasTestId { get; set; }

    public int PermitId { get; set; }
    public Permit? Permit { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? FunctionalLocationId { get; set; }
    public FunctionalLocation? FunctionalLocation { get; set; }

    // شرح دقیق محل فیزیکی انجام تست
    public string Location { get; set; } = string.Empty;

    public DateTime TestedAt { get; set; }

    public int TesterEmployeeId { get; set; }
    public Employee? TesterEmployee { get; set; }

    // شناسه‌ی آزاد دستگاه گاز-سنج قابل‌حمل - طبق پلن، Instrument entity برای این منظور مناسب
    // نیست (Instrument همیشه 1:1 با یک Equipment ثابت است، نه یک ابزار قابل‌حمل).
    public string? InstrumentTag { get; set; }
    public DateTime? InstrumentCalibrationDueDate { get; set; }

    // Passed, Failed, Invalidated
    public string Status { get; set; } = string.Empty;

    // از GasTestValidityRule محاسبه می‌شود؛ Expired به‌صورت Compute-on-read است، نه Status ذخیره‌شده.
    public DateTime? ValidUntil { get; set; }

    public string? InvalidationReason { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public List<GasReading> Readings { get; set; } = new();
}
