namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۱۰: «PermissionA CONFLICTS WITH PermissionB»
/// - اگر کاربری PermissionA را روی یک رکورد مشخص انجام داده باشد، همان کاربر نمی‌تواند PermissionB
/// را روی همان رکورد انجام دهد (مثلاً EngineeringDocument.Create در تعارض با
/// EngineeringDocument.Approve - خودتاییدی ممنوع). مدل داده‌محور/قابل‌تنظیم است، نه Hard-Code
/// شده در سرویس‌ها. تشخیص «قبلاً انجام داده» با کوئری روی AuthorizationAuditLog انجام می‌شود
/// (که خودِ AccessControlService.AuthorizeAsync دقیقاً به همین منظور با UserId+ResourceId+
/// Decision=Allow می‌نویسد) - نه AuditLog عمومی سراسری، چون آن فقط سطح Insert/Update/Delete
/// موجودیت را ثبت می‌کند و مشخص نمی‌کند دقیقاً کدام Action/Permission مجوزدهی شده بود.
/// </summary>
public class SoDPolicy
{
    public int SoDPolicyId { get; set; }

    public int PermissionAId { get; set; }
    public Permission? PermissionA { get; set; }

    public int PermissionBId { get; set; }
    public Permission? PermissionB { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
