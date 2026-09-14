namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS CMMS — Data Migration &amp; Controlled Data Onboarding»: همان الگوی
/// EquipmentImportBatch/FunctionalLocationImportBatch/EquipmentFunctionalLocationAssignmentBatch
/// (Staging → بازبینی کاربر → Commit صریح) - این‌جا برای اولین Entity در زنجیره‌ی وابستگی که
/// هنوز هیچ مسیر Import نداشت: MaintenanceHistory (رکورد تاریخچه‌ی نگهداری/تعمیر - Append-only،
/// طبق قرارداد صریح CLAUDE.md). هیچ Equipment/WorkOrder جدیدی ساخته نمی‌شود؛ فقط رکورد
/// MaintenanceHistory جدید، بعد از تایید صریح، درج می‌شود.
/// </summary>
public class MaintenanceHistoryImportBatch
{
    public int MaintenanceHistoryImportBatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int? ImportedById { get; set; }
    public Employee? ImportedBy { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    // Staging, Committed, Discarded
    public string Status { get; set; } = "Staging";

    public ICollection<MaintenanceHistoryImportRow> Rows { get; set; } = new List<MaintenanceHistoryImportRow>();
}
