using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001: پیاده‌سازی موتور مشترک Authorization &amp; Competency.
/// طبق تصمیم صریح کاربر: بررسی Plant/Area/Unit Scope در نبود داده (EmployeeAreaScope خالی)
/// Advisory است، نه Required - تا وقتی داده‌ای برای یک کارمند ثبت نشود، هیچ تاییدی را مسدود
/// نمی‌کند.
/// </summary>
public class PermitAuthorityService : IPermitAuthorityService
{
    private readonly PEMSDbContext _context;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly IWebAuthnService _webAuthnService;

    public PermitAuthorityService(PEMSDbContext context, IRoleModuleActionService roleModuleActionService, IWebAuthnService webAuthnService)
    {
        _context = context;
        _roleModuleActionService = roleModuleActionService;
        _webAuthnService = webAuthnService;
    }

    public async Task<ExecutionGateResultDto> EvaluateApproverEligibilityAsync(
        int userId, int? actorEmployeeId, IReadOnlyList<string> actorRoles, int permitId, string module, string action)
    {
        var result = new ExecutionGateResultDto();

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        result.Checks.Add(new ExecutionGateCheckDto
        {
            Name = "User Identity / Active Account",
            Category = "Required",
            Passed = user is { IsActive: true },
            Detail = user == null ? "کاربر پیدا نشد." : user.IsActive ? "حساب فعال است." : "حساب کاربری غیرفعال است."
        });

        var hasRoleAuthority = await _roleModuleActionService.HasActionAsync(actorRoles, module, action);
        result.Checks.Add(new ExecutionGateCheckDto
        {
            Name = "Assigned Role / Permit Role Authority",
            Category = "Required",
            Passed = hasRoleAuthority,
            Detail = hasRoleAuthority ? "نقش کاربر برای این اقدام مجاز است." : $"نقش کاربر اجازه‌ی «{action}» روی ماژول «{module}» را ندارد."
        });

        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId && x.IsActive);
        if (permit == null)
        {
            result.Checks.Add(new ExecutionGateCheckDto { Name = "Permit", Category = "Required", Passed = false, Detail = "Permit پیدا نشد." });
            result.Result = "Blocked";
            return result;
        }

        var competencyRequirement = await _context.PermitApprovalCompetencyRequirements
            .Include(x => x.RequiredCertification)
            .Include(x => x.RequiredQualification)
            .FirstOrDefaultAsync(x => x.IsActive && x.PermitType == permit.PermitType && x.Action == action);

        if (competencyRequirement != null)
        {
            var now = DateTime.Now;
            var ok = false;
            string detail;

            if (!actorEmployeeId.HasValue)
            {
                detail = "کارمند مرتبط با این کاربر پیدا نشد - ارزیابی Competency ممکن نیست.";
            }
            else if (competencyRequirement.RequiredCertificationId.HasValue)
            {
                var cert = await _context.EmployeeCertifications.FirstOrDefaultAsync(x =>
                    x.EmployeeId == actorEmployeeId.Value && x.IsActive
                    && x.CertificationId == competencyRequirement.RequiredCertificationId.Value
                    && (x.ExpiryDate == null || x.ExpiryDate >= now));
                ok = cert != null;
                detail = ok ? $"گواهینامه‌ی الزامی «{competencyRequirement.RequiredCertification?.Name}» معتبر است."
                    : $"گواهینامه‌ی الزامی «{competencyRequirement.RequiredCertification?.Name}» یافت نشد یا منقضی شده.";
            }
            else if (competencyRequirement.RequiredQualificationId.HasValue)
            {
                var qual = await _context.EmployeeQualifications.FirstOrDefaultAsync(x =>
                    x.EmployeeId == actorEmployeeId.Value && x.IsActive
                    && x.QualificationId == competencyRequirement.RequiredQualificationId.Value
                    && (x.ExpiryDate == null || x.ExpiryDate >= now));
                ok = qual != null;
                detail = ok ? $"صلاحیت الزامی «{competencyRequirement.RequiredQualification?.Name}» معتبر است."
                    : $"صلاحیت الزامی «{competencyRequirement.RequiredQualification?.Name}» یافت نشد یا منقضی شده.";
            }
            else
            {
                ok = true;
                detail = "قاعده‌ی Competency بدون گواهینامه/صلاحیت مشخص - نادیده گرفته شد.";
            }

            result.Checks.Add(new ExecutionGateCheckDto
            {
                Name = "Required Competency / Certification Validity",
                Category = "Conditional",
                Passed = ok,
                Detail = detail
            });
        }

        // طبق تصمیم کاربر: Advisory تا وقتی EmployeeAreaScope داده‌ای نداشته باشد.
        if (actorEmployeeId.HasValue)
        {
            var scope = await _context.EmployeeAreaScopes
                .FirstOrDefaultAsync(x => x.EmployeeId == actorEmployeeId.Value && x.IsActive);

            if (scope == null)
            {
                result.AdvisoryNotes.Add(new ExecutionGateCheckDto
                {
                    Name = "Plant / Area / Unit Scope",
                    Category = "Advisory",
                    Passed = false,
                    Detail = "محدوده‌ی عملیاتی برای این کارمند هنوز پیکربندی نشده - این بررسی مانع تایید نمی‌شود."
                });
            }
            else
            {
                var permitLocation = await _context.FunctionalLocations.FirstOrDefaultAsync(x => x.FunctionalLocationId == permit.FunctionalLocationId);
                var mismatch = permitLocation != null &&
                    ((scope.Plant != null && permitLocation.Plant != null && scope.Plant != permitLocation.Plant) ||
                     (scope.Area != null && permitLocation.Area != null && scope.Area != permitLocation.Area) ||
                     (scope.Unit != null && permitLocation.Unit != null && scope.Unit != permitLocation.Unit));

                result.AdvisoryNotes.Add(new ExecutionGateCheckDto
                {
                    Name = "Plant / Area / Unit Scope",
                    Category = "Advisory",
                    Passed = !mismatch,
                    Detail = mismatch ? "محدوده‌ی عملیاتی کارمند با محل این Permit همخوانی ندارد." : "محدوده‌ی عملیاتی همخوانی دارد یا محل Permit مشخص نشده."
                });
            }
        }

        result.Result = result.Checks.All(c => c.Passed) ? "Eligible" : "Blocked";
        return result;
    }

    public async Task<(bool Success, string? Error)> AuthorizeSensitiveActionAsync(
        int userId, int? actorEmployeeId, IReadOnlyList<string> actorRoles, int permitId,
        string module, string action, string? approvalStage,
        string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        var permit = await _context.Permits.FirstOrDefaultAsync(x => x.PermitId == permitId);

        var eligibility = await EvaluateApproverEligibilityAsync(userId, actorEmployeeId, actorRoles, permitId, module, action);

        if (eligibility.Result != "Eligible")
        {
            var reasons = string.Join("؛ ", eligibility.Checks.Where(c => !c.Passed).Select(c => c.Detail));
            await WriteAuditRecordAsync(permitId, userId, action, approvalStage, "None", null, "Blocked",
                actorRoles.FirstOrDefault(), permit, reasons);
            return (false, $"واجد شرایط نیستید: {reasons}");
        }

        // طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۲: بدون Passkey ثبت‌شده، اقدام حساس مسدود
        // می‌شود - طبق تصمیم صریح کاربر (بدون مسیر جایگزین با رمز عبور).
        if (string.IsNullOrWhiteSpace(webAuthnAssertionJson) || string.IsNullOrWhiteSpace(webAuthnOptionsJson))
        {
            var hasCredential = await _webAuthnService.HasAnyCredentialAsync(userId);
            var reason = hasCredential
                ? "احراز هویت Step-up (Passkey) برای این اقدام الزامی است."
                : "هیچ Passkey ای برای این حساب ثبت نشده - قبل از این اقدام، از بخش Manage Passkeys یک Passkey ثبت کنید.";
            await WriteAuditRecordAsync(permitId, userId, action, approvalStage, "None", null, "Blocked",
                actorRoles.FirstOrDefault(), permit, reason);
            return (false, reason);
        }

        var (assertSuccess, assertError, credentialDbId) = await _webAuthnService.CompleteAssertionAsync(
            userId, webAuthnAssertionJson, webAuthnOptionsJson);

        if (!assertSuccess)
        {
            await WriteAuditRecordAsync(permitId, userId, action, approvalStage, "WebAuthn", null, "Blocked",
                actorRoles.FirstOrDefault(), permit, $"احراز هویت Step-up ناموفق: {assertError}");
            return (false, $"احراز هویت Step-up ناموفق بود: {assertError}");
        }

        await WriteAuditRecordAsync(permitId, userId, action, approvalStage, "WebAuthn", credentialDbId, "Allowed",
            actorRoles.FirstOrDefault(), permit, null);

        return (true, null);
    }

    private async Task WriteAuditRecordAsync(
        int permitId, int userId, string action, string? approvalStage, string authMethod, int? credentialId,
        string decision, string? role, PEMS.Domain.Entities.Permit? permit, string? blockingReasons)
    {
        var snapshot = permit == null ? null : JsonSerializer.Serialize(new
        {
            permit.PermitType,
            permit.EquipmentId,
            permit.FunctionalLocationId,
            permit.Priority,
            permit.ValidFrom,
            permit.ValidTo,
            permit.Status
        });

        await _context.PermitApprovalAuditRecords.AddAsync(new PermitApprovalAuditRecord
        {
            PermitId = permitId,
            UserId = userId,
            Action = action,
            ApprovalStage = approvalStage,
            Timestamp = DateTime.Now,
            AuthenticationMethod = authMethod,
            CredentialReferenceId = credentialId,
            AuthorizationDecision = decision,
            Role = role,
            PermitSnapshotJson = snapshot,
            BlockingReasonsSummary = blockingReasons,
            AuditCorrelationId = Guid.NewGuid()
        });
        await _context.SaveChangesAsync();
    }
}
