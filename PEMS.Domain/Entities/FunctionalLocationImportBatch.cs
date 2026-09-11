namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «Functional Location Master Data Import»: همان الگوی EquipmentImportBatch/
/// MDRImportBatch (Staging → بازبینی کاربر → Commit) - ردیف‌ها تا زمانی که کاربر صریحاً
/// Commit نکند، وارد جدول اصلی FunctionalLocations نمی‌شوند.
/// </summary>
public class FunctionalLocationImportBatch
{
    public int FunctionalLocationImportBatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int? ImportedById { get; set; }
    public Employee? ImportedBy { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    // Staging, Committed, Discarded
    public string Status { get; set; } = "Staging";

    public ICollection<FunctionalLocationImportRow> Rows { get; set; } = new List<FunctionalLocationImportRow>();
}
