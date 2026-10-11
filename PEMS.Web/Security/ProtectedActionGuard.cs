using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Security;

public enum ProtectedActionOutcome
{
    /// <summary>فقط وقتی هر دو بررسی (نقش + آگاه-از-کاربر) صراحتاً Allowed بوده‌اند.</summary>
    Allowed,

    /// <summary>رد به‌خاطر تفکیک وظایف؛ کنترلر پیام دوستانه نشان می‌دهد (همچنان Deny است).</summary>
    DeniedBySoD,

    /// <summary>هر رد/خطای دیگر (نقش، Scope، هویت نامشخص، نتیجه‌ی غیرمنتظره، خطای ارزیابی) - کنترلر Forbid می‌کند.</summary>
    Denied
}

/// <summary>
/// گارد مشترک برای اقدامات حساس تدارکات (PurchaseOrder.Issue، ProcurementPackage.Closeout/AddApproval). Fail-Closed:
/// (۱) مجوز نقش (HasPermissionAsync، همان رفتار قبلی)؛ (۲) شناسه‌ی کاربر الزامی است؛ (۳) AuthorizeAsync آگاه-از-کاربر
/// (Scope + SoD + ردیف AuthorizationAuditLog)؛ (۴) فقط Allowed صریح عبور می‌دهد - هر نتیجه‌ی دیگر Denied است.
/// رد نقش-محور و هویت نامشخص هم در همان جدول AuthorizationAuditLog ثبت می‌شود (RecordDenialAsync) تا هر تلاش ردشده
/// یک ردیف داشته باشد؛ شکست در ثبت Audit هرگز به Allow تبدیل نمی‌شود.
/// </summary>
public static class ProtectedActionGuard
{
    public static async Task<ProtectedActionOutcome> EvaluateAsync(
        HttpContext? http, IPermissionService permissions, IAccessControlService access, ICurrentUserService user,
        string resource, string action, string resourceId)
    {
        var ip = http?.Connection.RemoteIpAddress?.ToString();
        var logger = http?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("PEMS.ProtectedAction");

        async Task DeniedAsync(AccessDecisionReason reason, string detail)
        {
            try { await access.RecordDenialAsync(user.UserId, resource, action, resourceId, ip, reason, detail); }
            catch (Exception ex) { logger?.LogError(ex, "Could not audit denied {Resource}.{Action}; request stays denied.", resource, action); }
        }

        if (!await permissions.HasPermissionAsync(user.Roles, resource, action))
        {
            await DeniedAsync(AccessDecisionReason.DeniedByRole, "رد بر اساس نقش‌های نشست جاری (بررسی فقط-نقش).");
            return ProtectedActionOutcome.Denied;
        }

        // بدون شناسه‌ی کاربر نه Scope/SoD قابل‌ارزیابی است نه اقدام قابل‌انتساب
        if (!user.UserId.HasValue)
        {
            await DeniedAsync(AccessDecisionReason.DeniedByPolicy, "شناسه‌ی کاربر احرازهویت‌شده در دسترس نیست.");
            return ProtectedActionOutcome.Denied;
        }

        AccessDecision? decision;
        try
        {
            decision = await access.AuthorizeAsync(new AccessControlRequest
            {
                UserId = user.UserId.Value, ResourceName = resource, ActionName = action, ResourceId = resourceId, IpAddress = ip
            });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Authorization evaluation failed for {Resource}.{Action}; request denied.", resource, action);
            return ProtectedActionOutcome.Denied;
        }

        if (decision is { Allowed: true, Reason: AccessDecisionReason.Allow }) return ProtectedActionOutcome.Allowed;
        return decision?.Reason == AccessDecisionReason.DeniedBySoD ? ProtectedActionOutcome.DeniedBySoD : ProtectedActionOutcome.Denied;
    }
}
