using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش M (Hold Model) ممیزی: هر چرخه‌ی Hold یک ردیف append-only جداست (نه فقط بازنویسی
/// یک bool) - تا تاریخچه‌ی کامل چندباره‌ی Hold/Release یک مدرک قابل بازسازی باشد. یک مدرک
/// «در Hold» است وقتی جدیدترین ردیف این جدول برایش ReleaseDate=null داشته باشد
/// (Document.IsOnHold دقیقاً همین را به‌صورت محاسبه‌شده روی خودِ Document نگه می‌دارد).
/// </summary>
public class DocumentHold
{
    [Key]
    public int DocumentHoldId { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime HoldDate { get; set; } = DateTime.Now;

    public int? HoldByEmployeeId { get; set; }
    public Employee? HoldByEmployee { get; set; }

    // مسئول رفع Hold (اختیاری - مثلاً دیسیپلینی که باید ورودی معلق را تکمیل کند)
    public int? ResponsibleDisciplineId { get; set; }
    public Discipline? ResponsibleDiscipline { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public int? ReleaseByEmployeeId { get; set; }
    public Employee? ReleaseByEmployee { get; set; }

    public string? ReleaseReason { get; set; }
}
