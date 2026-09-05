namespace PEMS.Domain.Entities;

/// <summary>
/// طبق درخواست: ایمپورت اکسل تجهیزات برای CMMS - همان الگوی "تایید قبل از ذخیره‌ی نهایی"
/// که در MDR/VDR Import استفاده شده (ردیف‌ها ابتدا Staging می‌شوند و تا کاربر تایید نکند،
/// وارد جدول اصلی Equipment نمی‌شوند).
/// </summary>
public class EquipmentImportBatch
{
    public int EquipmentImportBatchId { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int? ImportedById { get; set; }
    public Employee? ImportedBy { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    // Staging, Committed, Discarded
    public string Status { get; set; } = "Staging";

    public ICollection<EquipmentImportRow> Rows { get; set; } = new List<EquipmentImportRow>();
}
