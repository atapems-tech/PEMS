using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۴/۵/۱۳: موتور مشترک Authorization &amp; Competency -
/// نه فقط بررسی نقش، بلکه Role + Competency + Certification (با اعتبار) + Area Scope (فعلاً
/// Advisory). خروجی همون شکل ExecutionGateResultDto (CMMS-10f) است - نه یک DTO موازی جدید.
/// این سرویس هم برای نمایش (کدام تاییدکننده‌ها واجدشرایط‌اند) و هم به‌عنوان دروازه‌ی واقعی سمت
/// سرور، دقیقاً قبل از اجرای هر اقدام حساس، دوباره فراخوانی می‌شود.
/// </summary>
public interface IPermitAuthorityService
{
    Task<ExecutionGateResultDto> EvaluateApproverEligibilityAsync(
        int userId, int? actorEmployeeId, IReadOnlyList<string> actorRoles, int permitId, string module, string action);

    /// <summary>
    /// طبق سند PEMS-SEC-PERMIT-AUTH-001: نقطه‌ی ورود واحد برای هر اقدام حساس - Eligibility را
    /// ارزیابی می‌کند، Assertion مرحله‌ی Step-up را تایید می‌کند، و در هر دو حالت (مجاز/رد‌شده)
    /// یک PermitApprovalAuditRecord می‌نویسد. سرویس‌های فراخوان (Permit/Isolation/Simops/
    /// ExecutionGate) فقط همین یک متد را قبل از منطق موجود خودشان صدا می‌زنند - بدون تکرار
    /// منطق Eligibility/Audit در هرکدام.
    /// </summary>
    Task<(bool Success, string? Error)> AuthorizeSensitiveActionAsync(
        int userId, int? actorEmployeeId, IReadOnlyList<string> actorRoles, int permitId,
        string module, string action, string? approvalStage,
        string? webAuthnAssertionJson, string? webAuthnOptionsJson);
}
