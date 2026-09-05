using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک نشست Import اکسل VDR. طبق الزام "Approval Before Final Storage"، ردیف‌های خوانده‌شده
/// ابتدا در VDRImportRow (Staging) ذخیره می‌شوند و تا زمانی که کاربر دکمه‌ی Commit را نزند
/// وارد جداول اصلی (VDRItem/VDRSubmission) نمی‌شوند.
/// </summary>
public class VDRImportBatch
{
    [Key]
    public int VDRImportBatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? ImportedById { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    public int RowsTotal { get; set; }

    public int RowsValid { get; set; }

    public int RowsWarning { get; set; }

    public int RowsError { get; set; }

    public int RowsDuplicate { get; set; }

    // Staged, Committed, Discarded
    public string Status { get; set; } = "Staged";

    public DateTime? CommittedDate { get; set; }

    public ICollection<VDRImportRow> Rows { get; set; } = new List<VDRImportRow>();
}
