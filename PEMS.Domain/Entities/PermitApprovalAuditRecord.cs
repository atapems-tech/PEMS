namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۱۰/۱۱: رکورد مستقل رویداد احراز هویت/اختیار برای هر
/// اقدام حساس روی Permit - جدا از AuditLog عمومی (که فقط تغییرات فیلد را ثبت می‌کند، نه روش
/// احراز هویت/تصمیم مجوزدهی). روی هر تلاش واقعی (مجاز یا رد‌شده) نوشته می‌شود - نه روی هر بار
/// مشاهده‌ی صفحه. PermitSnapshotJson نسخه‌ی حداقلی حفاظت از Revision را فراهم می‌کند (بند ۹
/// پلن) - بدون سیستم Versioning کامل.
/// </summary>
public class PermitApprovalAuditRecord
{
    public int PermitApprovalAuditRecordId { get; set; }

    public int PermitId { get; set; }
    public Permit? Permit { get; set; }

    public int UserId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? ApprovalStage { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Password, WebAuthn
    public string AuthenticationMethod { get; set; } = string.Empty;
    public int? CredentialReferenceId { get; set; }
    public WebAuthnCredential? CredentialReference { get; set; }

    // Allowed, Blocked
    public string AuthorizationDecision { get; set; } = string.Empty;

    public string? Role { get; set; }
    public int? CompetencyReferenceId { get; set; }
    public int? CertificationReferenceId { get; set; }
    public string? AreaScope { get; set; }
    public string? SessionReference { get; set; }

    public string? PermitSnapshotJson { get; set; }

    public string? BlockingReasonsSummary { get; set; }

    public Guid AuditCorrelationId { get; set; } = Guid.NewGuid();
}
