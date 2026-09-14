namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف Staging برای یک رکورد پیشنهادی MaintenanceHistory. طبق همان اصل «Suggestion در
/// برابر رکورد Committed» - تا وقتی Batch والد Commit نشود، هیچ رکورد MaintenanceHistory واقعی
/// ساخته نمی‌شود؛ این ردیف فقط یک پیشنهاد قابل‌بازبینی است.
/// </summary>
public class MaintenanceHistoryImportRow
{
    public int MaintenanceHistoryImportRowId { get; set; }

    public int MaintenanceHistoryImportBatchId { get; set; }
    public MaintenanceHistoryImportBatch? Batch { get; set; }

    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    // مقادیر خام ستون‌های اکسل - طبق بند «Traceability» همون الگوی PayloadJson در
    // EquipmentFunctionalLocationAssignmentRow/FunctionalLocationImportRow
    public string PayloadJson { get; set; } = string.Empty;

    public bool IncludeInCommit { get; set; } = true;

    // Valid, Warning, Error - همون قرارداد Import‌های موجود
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    // New, Duplicate, UnresolvedEquipment, ConflictEquipment, ValidationError - طبق بند
    // «Source Identifier and Idempotency Rules» سند وظیفه: چون MaintenanceHistory هیچ کلید
    // کسب‌وکاری طبیعی از قبل ندارد (بر خلاف Equipment.TagNumber/FunctionalLocation.Code)، کوچک‌ترین
    // قاعده‌ی قطعی لازم برقرار شد - دقیقاً همون الگوی MaintenanceStrategy (Equipment+FailureModeCode):
    // یک کلید ترکیبی از فیلدهای خودِ رکورد (Equipment+ExecutionDate+ActionTaken+Technician).
    public string Status { get; set; } = "UnresolvedEquipment";

    public int? ResolvedEquipmentId { get; set; }

    // طبق بند «WorkOrder اختیاری/Soft Reference»: هرگز مانع Commit نمی‌شود - اگر ستون
    // WorkOrderNumber در فایل باشد ولی تطبیق پیدا نشود/مبهم باشد، فقط یک هشدار ثبت می‌شود و
    // WorkOrderId رکورد نهایی خالی می‌ماند (schema هم این فیلد را nullable تعریف کرده).
    public int? ResolvedWorkOrderId { get; set; }

    // طبق بند «Idempotency»: اگر رکوردی با همین کلید ترکیبی از قبل وجود دارد، شناسه‌اش این‌جا
    // ثبت می‌شود - برای گزارش/بازبینی، نه برای بازنویسی (بازنویسی هرگز اتفاق نمی‌افتد).
    public int? DuplicateOfMaintenanceHistoryId { get; set; }

    // فقط بعد از Commit واقعی مقداردهی می‌شود
    public int? CommittedMaintenanceHistoryId { get; set; }
}
