namespace PEMS.Domain.Entities;

/// <summary>
/// ارجاع یک مدرک برای بازبینی IDC (Inter Discipline Check) به یک دیسیپلین مشخص. این تصمیم
/// را سازنده‌ی مدرک هنگام Submit می‌گیرد - نه یک قاعده‌ی ثابت سیستمی. هر ردیف یعنی «این
/// دیسیپلین باید این مدرک را بازبینی کند».
/// </summary>
public class DocumentIdcReferral
{
    public int DocumentIdcReferralId { get; set; }

    public int DocumentId { get; set; }

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public int? CreatedById { get; set; }
}
