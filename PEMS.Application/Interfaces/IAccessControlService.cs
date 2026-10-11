using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۱۶: سرویس مرکزی تصمیم مجوزدهی -
/// Authorize(User, Resource, Action, ResourceId, Context). نام «AccessControl» عمداً انتخاب شده
/// تا با Microsoft.AspNetCore.Authorization.IAuthorizationService تداخل نکند.
///
/// IPermissionService.HasPermissionAsync و IRoleModuleActionService.HasActionAsync (مکانیزم‌های
/// موجود A/B) این‌جا را به‌عنوان موتور تصمیم واقعی صدا می‌زنند (بدون تغییر در فراخوانی‌کننده‌های
/// ~۱۶ کنترلر موجود) - AuthorizeForRolesAsync دقیقاً برای همین سازگاری رفتاری بدون تغییر است.
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// تصمیم کامل و آگاه از کاربر - نقش‌ها از AspNetUserRoles ∪ RoleAssignment فعال/غیرمنقضی
    /// حل می‌شوند، یک ردیف AuthorizationAuditLog ثبت می‌شود. در فاز ۱، ارزیابی Scope/SoD/
    /// Workflow-State همیشه عبور می‌کند (هیچ فراخوان فعلی به Scope واقعی نیاز ندارد) - در فاز ۲
    /// واقعی می‌شود.
    /// </summary>
    Task<AccessDecision> AuthorizeAsync(AccessControlRequest request);

    /// <summary>
    /// تصمیم فقط بر اساس فهرست نقش (بدون Scope/SoD/انقضا/ثبت Audit) - صرفاً برای سازگاری
    /// رفتاری با HasPermissionAsync/HasActionAsync موجود که امروز فقط لیست نقش در اختیار دارند،
    /// نه UserId.
    /// </summary>
    Task<AccessDecision> AuthorizeForRolesAsync(IEnumerable<string> roles, string resourceName, string actionName);

    /// <summary>
    /// ثبت یک تلاش «ردشده» که پیش از رسیدن به AuthorizeAsync متوقف شده (رد فقط-نقش، هویت نامشخص) در همان جدول
    /// AuthorizationAuditLog. Default Interface Method تا پیاده‌سازی‌های دیگر/Fake ها نشکنند؛ تصمیم را تغییر نمی‌دهد.
    /// </summary>
    Task RecordDenialAsync(int? userId, string resourceName, string actionName, string? resourceId, string? ipAddress,
        AccessDecisionReason reason, string detail) => Task.CompletedTask;
}
