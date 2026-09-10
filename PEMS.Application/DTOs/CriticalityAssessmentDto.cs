namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 7.6»: یک سطر در Assessment Queue - صفحه‌بندی‌شده، هرگز کل ناوگان را
/// بارگذاری نمی‌کند.
/// </summary>
public class CriticalityAssessmentListDto
{
    public int CriticalityAssessmentId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = string.Empty;
    public string? EquipmentClassName { get; set; }
    public string FrameworkVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProposedCriticalityClass { get; set; }
    public string? PreparedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CriticalityAssessmentEvidenceDto
{
    public int CriticalityAssessmentEvidenceId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceIdentifier { get; set; }
    public string? Reference { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// طبق بند ۱۳: تاریخچه‌ی کامل - Assessment جاری + زنجیره‌ی SUPERSEDED قبلی (هرگز حذف نمی‌شوند).
/// </summary>
public class CriticalityAssessmentDetailDto
{
    public int CriticalityAssessmentId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = string.Empty;
    public string? EquipmentClassName { get; set; }

    public string FrameworkVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AssessmentDate { get; set; }
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ReasonForChange { get; set; }

    public int? SafetyScore { get; set; }
    public int? ProductionScore { get; set; }
    public int? EnvironmentScore { get; set; }
    public int? CalculatedMaxScore { get; set; }
    public string? ProposedCriticalityClass { get; set; }
    public string? Rationale { get; set; }

    public List<CriticalityAssessmentEvidenceDto> Evidence { get; set; } = new();

    public int? SupersededByAssessmentId { get; set; }
    public List<CriticalityAssessmentListDto> PriorAssessments { get; set; } = new();
}

/// <summary>
/// طبق بند ۲۱-الف: هرگز درصدهای گمراه‌کننده - هر عدد یک COUNT واقعی است.
/// </summary>
public class CriticalityDashboardDto
{
    public int TotalActiveEquipment { get; set; }
    public int Assessed { get; set; }
    public int Approved { get; set; }
    public int UnderReview { get; set; }
    public int Draft { get; set; }
    public int Superseded { get; set; }
    public int Unassessed => TotalActiveEquipment - Assessed;

    // طبق بند ۹: توزیع فقط از روی EquipmentCriticality فعلیِ منتشرشده (منتشرشده = رکورد
    // فعلی معتبر، نه هر پیش‌نویس) - هرگز یک عدد جعلی نیست.
    public int PublishedCriticalityA { get; set; }
    public int PublishedCriticalityB { get; set; }
    public int PublishedCriticalityC { get; set; }
    public int PublishedCriticalityOther { get; set; }

    public string FrameworkVersion { get; set; } = string.Empty;
}
