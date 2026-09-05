using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک اجرای «Extract as Table» روی یک پیوست - طبق همان الگوی Stage-سپس-تایید که در
/// EquipmentImportBatch/MDRImportBatch استفاده شده. هیچ ردیفی مستقیم در Equipment/Instrument
/// نمی‌نشیند؛ فقط بعد از تایید صریح کاربر در صفحه‌ی Review (طبق همان قاعده‌ی
/// RelationshipService.ConfirmSuggestedLinkAsync - همیشه با تایید انسان، هرگز خودکار).
/// </summary>
public class TableExtractionBatch
{
    [Key]
    public int TableExtractionBatchId { get; set; }

    public int SourceAttachmentId { get; set; }
    public DocumentAttachment? SourceAttachment { get; set; }

    public string SourceFileName { get; set; } = string.Empty;

    // Deterministic | AI
    public string ExtractionMethod { get; set; } = "Deterministic";

    public int TotalRows { get; set; }

    public int? CreatedById { get; set; }
    public Employee? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<TableExtractionRow> Rows { get; set; } = new List<TableExtractionRow>();
}
