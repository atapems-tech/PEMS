namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۹:
/// دقیقاً همون الگوی MaintenanceHistoryImportBatch (Stage → Review/Toggle → Commit/Discard) -
/// برای تاریخچه‌ی قرائت‌های پایش وضعیت (ConditionReading). هیچ ConditionMonitoringPoint جدیدی
/// نمی‌سازد؛ نقطه‌ی پایش باید از قبل وجود داشته باشد - فقط قرائت‌های Append-only درج می‌شوند.
/// </summary>
public class ConditionReadingImportBatch
{
    public int ConditionReadingImportBatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int? ImportedById { get; set; }
    public Employee? ImportedBy { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    // Staging, Committed, Discarded
    public string Status { get; set; } = "Staging";

    public ICollection<ConditionReadingImportRow> Rows { get; set; } = new List<ConditionReadingImportRow>();
}
