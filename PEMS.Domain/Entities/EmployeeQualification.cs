using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۱۸ سند: نمونه‌ی صلاحیت یک کارمند مشخص - append-only مثل تاریخچه‌ی نرخ دستمزد،
/// هرگز بازنویسی نمی‌شود.
/// </summary>
public class EmployeeQualification : BaseEntity
{
    public int EmployeeQualificationId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int QualificationId { get; set; }
    public Qualification? Qualification { get; set; }

    public string? Issuer { get; set; }

    public DateTime IssueDate { get; set; } = DateTime.Now;

    public DateTime? ExpiryDate { get; set; }

    public string? EvidenceDocumentPath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
