using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class InspectionChecklist
{
    [Key]
    public int InspectionChecklistId { get; set; }

    // رکورد بازرسی
    public int InspectionRecordId { get; set; }

    public InspectionRecord? InspectionRecord { get; set; }

    // آیتم چک لیست
    public string ItemName { get; set; } = string.Empty;

    // معیار پذیرش
    public string? Requirement { get; set; }

    // نتیجه
    // Pass / Fail / Warning
    public string? Result { get; set; }

    // آیا قبول شده؟
    public bool IsPassed { get; set; }

    // توضیحات
    public string? Remarks { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}