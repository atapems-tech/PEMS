namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture: پیوند Role (از ASP.NET Identity - ApplicationRole
/// در PEMS.Persistence.Identity) به یک Permission. عمداً بدون Navigation Property به ApplicationRole
/// (لایه‌ی Domain نباید به Persistence.Identity وابسته شود) - پیوند واقعی در EF Configuration
/// (لایه‌ی Persistence) با HasOne&lt;ApplicationRole&gt;() برقرار می‌شود، دقیقاً هم‌الگو با
/// WebAuthnCredential/PermitApprovalAuditRecord که به همین شکل به ApplicationUser وصل‌اند.
/// مسطح/بدون Scope است - Scope خاصیت تخصیص کاربر به نقش است (RoleAssignment)، نه خودِ تعریف نقش.
/// </summary>
public class RolePermissionGrant
{
    public int RolePermissionGrantId { get; set; }

    // FK به ApplicationRole.Id (AspNetRoles)
    public int RoleId { get; set; }

    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
