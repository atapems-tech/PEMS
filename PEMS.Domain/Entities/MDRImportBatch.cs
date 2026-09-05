using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک نشست Import اکسل MDR (Master/Engineering Document Register). دقیقاً همان الگوی
/// VDRImportBatch: طبق "Approval Before Final Storage"، ردیف‌ها ابتدا Staging می‌شوند و تا
/// Commit صریح کاربر وارد جدول اصلی EngineeringDocuments نمی‌شوند.
/// </summary>
public class MDRImportBatch
{
    [Key]
    public int MDRImportBatchId { get; set; }

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

    // گزارش پوشش هدر به‌صورت JSON (لیست رشته‌ای از ستون‌های شناسایی‌نشده به تفکیک شیت).
    // هدف: وقتی مشاور/فایل بعدی ستونی را جابجا یا تغییرنام دهد، به‌جای گم‌شدن بی‌صدای داده،
    // همینجا به کاربر نشان داده شود.
    public string? HeaderWarningsJson { get; set; }

    public ICollection<MDRImportRow> Rows { get; set; } = new List<MDRImportRow>();
}
