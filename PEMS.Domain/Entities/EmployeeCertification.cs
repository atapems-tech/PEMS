using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۱۹ سند: گواهینامه‌ی مشخص یک کارمند - «انقضای گواهینامه باید قابل‌تشخیص باشد»،
/// وضعیت (Overdue/DueSoon/OK) دقیقاً با همون منطق سررسید کالیبراسیون ابزاردقیق محاسبه می‌شود.
/// </summary>
public class EmployeeCertification : BaseEntity
{
    public int EmployeeCertificationId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int CertificationId { get; set; }
    public Certification? Certification { get; set; }

    public string? CertificateNumber { get; set; }

    public string? Issuer { get; set; }

    public DateTime IssueDate { get; set; } = DateTime.Now;

    public DateTime? ExpiryDate { get; set; }

    public string? EvidenceDocumentPath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
