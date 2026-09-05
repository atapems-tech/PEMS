using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۸: فهرست مشترک انواع
/// صلاحیت (مثلاً «تکنسین مجاز تعمیر پمپ») - نمونه‌ی هر کارمند در EmployeeQualification.
/// </summary>
public class Qualification : BaseEntity
{
    public int QualificationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
