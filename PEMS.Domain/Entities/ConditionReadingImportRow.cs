namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف Staging برای یک قرائت پیشنهادی ConditionReading. طبق همون اصل «Suggestion در برابر
/// رکورد Committed» - تا وقتی Batch والد Commit نشود، هیچ ConditionReading واقعی ساخته نمی‌شود.
/// کلید تشخیص تکرار (Idempotency) = (ConditionMonitoringPointId, ReadingDate دقیق) - چون یک
/// نقطه‌ی پایش می‌تواند چند قرائت واقعی در یک روز داشته باشد (مثلاً SCADA)، نه فقط تاریخ خالی.
/// </summary>
public class ConditionReadingImportRow
{
    public int ConditionReadingImportRowId { get; set; }

    public int ConditionReadingImportBatchId { get; set; }
    public ConditionReadingImportBatch? Batch { get; set; }

    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    // مقادیر خام ستون‌های اکسل - همون الگوی PayloadJson در MaintenanceHistoryImportRow
    public string PayloadJson { get; set; } = string.Empty;

    public bool IncludeInCommit { get; set; } = true;

    // Valid, Warning, Error
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    // New, Duplicate, UnresolvedPoint, ConflictPoint, ValidationError
    public string Status { get; set; } = "UnresolvedPoint";

    public int? ResolvedConditionMonitoringPointId { get; set; }

    // طبق بند «Idempotency»: اگر قرائتی با همین نقطه‌ی پایش/تاریخ دقیق از قبل وجود دارد، شناسه‌اش
    // این‌جا ثبت می‌شود - برای گزارش/بازبینی، نه بازنویسی (بازنویسی هرگز اتفاق نمی‌افتد).
    public int? DuplicateOfConditionReadingId { get; set; }

    // فقط بعد از Commit واقعی مقداردهی می‌شود
    public int? CommittedConditionReadingId { get; set; }
}
