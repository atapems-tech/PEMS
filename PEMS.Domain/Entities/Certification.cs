using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۱۹ سند: فهرست مشترک انواع گواهینامه (مثلاً «NDT Level II») - نمونه‌ی هر کارمند
/// در EmployeeCertification با شماره‌ی گواهی و تاریخ انقضای مشخص.
/// </summary>
public class Certification : BaseEntity
{
    public int CertificationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
