namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS CMMS — Equipment Functional Location Assignment &amp; Import»: همان الگوی
/// EquipmentImportBatch/FunctionalLocationImportBatch (Staging → بازبینی کاربر → Commit
/// صریح) - این‌جا هیچ Equipment/FunctionalLocation جدیدی ساخته نمی‌شود؛ فقط رابطه‌ی
/// Equipment.FunctionalLocationId موجود، بر اساس تطبیق قطعی Tag/Code، پیشنهاد و بعد از تایید
/// صریح مهندس/کاربر مجاز اعمال می‌شود.
/// </summary>
public class EquipmentFunctionalLocationAssignmentBatch
{
    public int EquipmentFunctionalLocationAssignmentBatchId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int? ImportedById { get; set; }
    public Employee? ImportedBy { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    // Staging, Committed, Discarded
    public string Status { get; set; } = "Staging";

    public ICollection<EquipmentFunctionalLocationAssignmentRow> Rows { get; set; } = new List<EquipmentFunctionalLocationAssignmentRow>();
}
