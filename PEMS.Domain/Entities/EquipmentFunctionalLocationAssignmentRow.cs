namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف Staging برای یک پیشنهاد تخصیص Equipment→FunctionalLocation. طبق بند «Suggestion در
/// برابر Committed Assignment» سند وظیفه: تا وقتی Batch والد Commit نشود، هیچ تغییری روی
/// Equipment واقعی اعمال نمی‌شود - این ردیف فقط یک پیشنهاد قابل‌بازبینی است.
/// </summary>
public class EquipmentFunctionalLocationAssignmentRow
{
    public int EquipmentFunctionalLocationAssignmentRowId { get; set; }

    public int EquipmentFunctionalLocationAssignmentBatchId { get; set; }
    public EquipmentFunctionalLocationAssignmentBatch? Batch { get; set; }

    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    // مقادیر خام ستون‌های اکسل (EquipmentTag/FunctionalLocationCode + ستون‌های اختیاری
    // Plant/Area/Unit برای اعتبارسنجی زمینه‌ای) - طبق بند «Traceability» همون الگوی
    // FunctionalLocationImportRowPayload
    public string PayloadJson { get; set; } = string.Empty;

    public bool IncludeInCommit { get; set; } = true;

    // Valid, Warning, Error - همون قرارداد دو Import موجود
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    // طبق بند «Suggestion Confidence» سند وظیفه - وضعیت قطعی و قابل‌توضیح، نه امتیاز عددی:
    // Exact, AlreadyAssigned, NoChange, UnresolvedEquipment, UnresolvedFunctionalLocation,
    // ConflictEquipment, ConflictFunctionalLocation, ValidationError
    public string Status { get; set; } = "UnresolvedEquipment";

    public int? ResolvedEquipmentId { get; set; }
    public int? ResolvedFunctionalLocationId { get; set; }

    // مقدار FunctionalLocationId فعلی تجهیز در لحظه‌ی Staging - طبق بند «Existing Assignment
    // Protection»، برای تشخیص تخصیص موجود از عدم‌تخصیص، مستقل از آنچه در لحظه‌ی Commit واقعاً
    // در دیتابیس است (که ممکنه بین Staging و Commit توسط یک مسیر دیگه تغییر کرده باشه).
    public int? PreviousFunctionalLocationId { get; set; }

    // طبق بند «Existing Assignment Protection»: پیش‌فرض false - کاربر باید صریحاً انتخاب کند
    // که یک تخصیص موجود و متفاوت جایگزین شود.
    public bool ReplaceExistingAssignment { get; set; }

    // فقط بعد از Commit واقعی true می‌شود - برای گزارش نهایی/Idempotency، جدا از IncludeInCommit
    public bool CommittedAssignment { get; set; }
}
