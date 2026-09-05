namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۱۶/۲۱: ثبت هر تصمیم مجوزدهی (نه فقط تغییر
/// رکورد - AuditLog سراسری موجود آن را پوشش می‌دهد). جدا از AuditLog عمومی چون یک تصمیم DENY
/// هیچ تغییری در دیتابیس ایجاد نمی‌کند تا SaveChangesAsync آن را خودکار ثبت کند.
/// عمداً بدون Navigation/FK اجباری به User - دقیقاً هم‌الگو با AuditLog.UserId (nullable ساده) -
/// ثبت Audit هرگز نباید به خاطر یک محدودیت FK شکست بخورد.
/// </summary>
public class AuthorizationAuditLog
{
    public int AuthorizationAuditLogId { get; set; }

    public int? UserId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public string ActionName { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public string? ScopeType { get; set; }
    public string? ScopeValue { get; set; }

    // Allow, DeniedByRole, DeniedByScope, DeniedBySoD, DeniedByWorkflowState, DeniedByOwnership,
    // DeniedByPolicy
    public string Decision { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string? IpAddress { get; set; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
