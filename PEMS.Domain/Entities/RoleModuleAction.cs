namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10a ADR-02: پایه‌ی مجوزدهی عمومی Role/Module/Action - جایگزین RolePermission
/// نیست (که برای ۱۲ ماژول موجود دست‌نخورده باقی می‌ماند)، بلکه یک مکانیزم موازی و افزودنی
/// برای ماژول‌هایی است که مجموعه‌عمل‌شان از View/Create/Edit/Delete/Approve فراتر می‌رود
/// (Permit با ~۱۴ عمل: Submit/Approve/Reject/Issue/Suspend/Resume/Close/Cancel/Revoke...).
/// عمداً بدون ستون Scope در این فاز - طبق بند ۱۴ سند: فقط نقطه‌ی اتصال ایجاد می‌شود، نه
/// برنامه‌ی کامل مجوزدهی که در CMMS-10b می‌آید.
/// </summary>
public class RoleModuleAction
{
    public int RoleModuleActionId { get; set; }

    // باید با نام‌های SystemRoles یکی باشد
    public string RoleName { get; set; } = string.Empty;

    // مثلاً: Permit
    public string Module { get; set; } = string.Empty;

    // مثلاً: View, Create, Submit, Approve, Reject, Issue, Suspend, Resume, Close, Cancel, Revoke
    public string Action { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
