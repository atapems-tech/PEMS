using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// دیسیپلین‌های QC/QA - عمداً جدول جداگانه از Discipline (دیسیپلین مهندسی). دیسیپلین QC بر
/// اساس روش/حوزه‌ی بازرسی تعریف می‌شود (NDT, Welding, Coating, ...)، نه بر اساس تخصص طراحی
/// مهندسی - طبق تاکید صریح: "این بخش دیسیپلین‌های خودش رو داره".
/// </summary>
public class QCDiscipline : BaseEntity
{
    public int QCDisciplineId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
