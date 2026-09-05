using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۵: منبع واحد اینکه کدام Certification/Qualification
/// برای اقدام مشخصی روی نوع خاصی از Permit الزامی است - دقیقاً همون الگوی GasParameterLimit/
/// PermitExecutionRequirement. اگر برای یک جفت (PermitType, Action) ردیفی وجود نداشته باشد،
/// هیچ Competency ای الزامی نیست (Conditional، نه پیش‌فرض الزامی حدس‌زده‌شده).
/// </summary>
public class PermitApprovalCompetencyRequirement : BaseEntity
{
    public int PermitApprovalCompetencyRequirementId { get; set; }

    public string PermitType { get; set; } = string.Empty;

    // Approve, Reject, Close, Verify, Confirm, Resolve, AcceptRisk, StartWork, EmergencyOverride
    public string Action { get; set; } = string.Empty;

    public int? RequiredCertificationId { get; set; }
    public Certification? RequiredCertification { get; set; }

    public int? RequiredQualificationId { get; set; }
    public Qualification? RequiredQualification { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
