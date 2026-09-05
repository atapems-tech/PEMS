using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture (فاز ۱): موتور تصمیم مرکزی مجوزدهی -
/// AuthorizeForRolesAsync عمداً باید رفتار HasPermissionAsync (PermissionService) و
/// HasActionAsync (RoleModuleActionService) را دقیقاً بازتولید کند (اثبات‌شده با
/// AccessControlParityTests) چون آن دو سرویس در فاز ۱ پیاده‌سازی خودشان را به این‌جا واگذار
/// می‌کنند - هیچ کنترلر موجودی نباید رفتارش تغییر کند. به همین دلیل DecideAsync (مسیر فقط-نقش،
/// بدون Scope/SoD) در فاز ۲ دست‌نخورده می‌ماند؛ Scope/SoD واقعی فقط در مسیر جدید AuthorizeAsync
/// (آگاه از کاربر) اضافه شده - مسیری که در فاز ۱ هیچ فراخوان واقعی نداشت، پس افزودن منطق جدید
/// این‌جا هیچ رفتار موجودی را نمی‌شکند.
///
/// نکته‌ی امنیتی حیاتی (طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۱۲، عیناً از RoleModuleActionService
/// کپی شده): Bypass عمومی SystemAdmin برای ExecutionGate.EmergencyOverride عمداً اعمال نمی‌شود
/// (Break-Glass authority نباید ضمنی باشد) - این استثنا در هر دو مسیر (فقط-نقش و آگاه-از-کاربر)
/// باید دقیقاً حفظ شود.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private readonly PEMSDbContext _context;

    public AccessControlService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<AccessDecision> AuthorizeForRolesAsync(IEnumerable<string> roles, string resourceName, string actionName)
    {
        return await DecideAsync(roles.ToList(), resourceName, actionName);
    }

    public async Task<AccessDecision> AuthorizeAsync(AccessControlRequest request)
    {
        var decision = await DecideForUserAsync(request.UserId, request.ResourceName, request.ActionName, request.ResourceId, request.Context);

        _context.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            UserId = request.UserId,
            ResourceName = request.ResourceName,
            ActionName = request.ActionName,
            ResourceId = request.ResourceId,
            ScopeType = decision.MatchedScopeType,
            ScopeValue = decision.MatchedScopeValue,
            Decision = decision.Reason.ToString(),
            Reason = decision.Detail,
            IpAddress = request.IpAddress,
            Timestamp = DateTime.Now
        });
        await _context.SaveChangesAsync();

        return decision;
    }

    private record RoleGrant(int RoleId, string RoleName, string ScopeType, string? ScopeValue);

    /// <summary>
    /// طبق سند بخش ۱۱ (Scope-Based Access) + بخش ۱۰ (Separation of Duties) - مسیر کامل و
    /// آگاه از کاربر: نقش (با احترام به Bypass/Break-Glass موجود در DecideAsync)، سپس Scope
    /// دقیقاً روی همان RoleAssignment که مجوز را داده (نه هر نقشی که کاربر دارد)، سپس SoD روی
    /// همان ResourceId از تاریخچه‌ی واقعی AuthorizationAuditLog.
    /// </summary>
    private async Task<AccessDecision> DecideForUserAsync(
        int userId, string resourceName, string actionName, string? resourceId, IDictionary<string, string>? context)
    {
        var grants = await ResolveUserRoleGrantsAsync(userId);

        var isBreakGlassAction = resourceName == "ExecutionGate" && actionName == "EmergencyOverride";
        if (!isBreakGlassAction && grants.Any(g => g.RoleName == SystemRoles.SystemAdmin))
            return AccessDecision.Allow(SystemRoles.SystemAdmin, "SystemAdmin bypass");

        var permission = await _context.Permissions
            .FirstOrDefaultAsync(p => p.ResourceName == resourceName && p.ActionName == actionName && p.IsActive);

        if (permission == null)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, $"مجوزی برای «{resourceName}.{actionName}» تعریف نشده است.");

        if (grants.Count == 0)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, "کاربر هیچ نقشی ندارد.");

        var roleIds = grants.Select(g => g.RoleId).Distinct().ToList();
        var grantedRoleIds = await _context.RolePermissionGrants
            .Where(g => g.IsActive && g.PermissionId == permission.PermissionId && roleIds.Contains(g.RoleId))
            .Select(g => g.RoleId)
            .ToListAsync();

        if (grantedRoleIds.Count == 0)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, $"هیچ‌کدام از نقش‌های کاربر مجوز «{permission.Name}» ندارند.");

        var qualifyingGrants = grants.Where(g => grantedRoleIds.Contains(g.RoleId)).ToList();

        // طبق بخش ۱۱ سند: نقش سراسری (Global - مثلاً از AspNetUserRoles مستقیم) همیشه عبور
        // می‌کند؛ نقش Scope‌دار فقط وقتی که مقدار Scope درخواست با ScopeValue تخصیص یکی باشد
        var scopeMatch = qualifyingGrants.FirstOrDefault(g =>
            g.ScopeType == "Global" ||
            (context != null && context.TryGetValue(g.ScopeType, out var ctxVal)
                && string.Equals(ctxVal, g.ScopeValue, StringComparison.OrdinalIgnoreCase)));

        if (scopeMatch == null)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByScope,
                $"نقش‌های کاربر مجوز «{permission.Name}» را فقط در محدوده‌ی خاصی دارند که با درخواست فعلی مطابقت ندارد.");

        // طبق بخش ۱۰ سند (Separation of Duties): آیا همین کاربر قبلاً یک Action متعارض را روی
        // همین دقیقاً رکورد انجام داده؟ فقط وقتی ResourceId مشخص باشد قابل‌بررسی است
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            var conflictingPolicies = await _context.SoDPolicies
                .Include(p => p.PermissionA)
                .Include(p => p.PermissionB)
                .Where(p => p.IsActive &&
                    (p.PermissionAId == permission.PermissionId || p.PermissionBId == permission.PermissionId))
                .ToListAsync();

            foreach (var policy in conflictingPolicies)
            {
                var pairedPermission = policy.PermissionAId == permission.PermissionId ? policy.PermissionB : policy.PermissionA;
                if (pairedPermission == null) continue;

                var priorAllow = await _context.AuthorizationAuditLogs.AnyAsync(a =>
                    a.UserId == userId &&
                    a.ResourceId == resourceId &&
                    a.ResourceName == pairedPermission.ResourceName &&
                    a.ActionName == pairedPermission.ActionName &&
                    a.Decision == nameof(AccessDecisionReason.Allow));

                if (priorAllow)
                    return AccessDecision.Deny(AccessDecisionReason.DeniedBySoD,
                        $"طبق سیاست تفکیک وظایف، کاربری که «{pairedPermission.Name}» را روی این رکورد انجام داده نمی‌تواند «{permission.Name}» را هم روی همان رکورد انجام دهد.");
            }
        }

        return AccessDecision.Allow(scopeMatch.RoleName, $"از طریق نقش «{scopeMatch.RoleName}» (Scope: {scopeMatch.ScopeType})",
            scopeMatch.ScopeType, scopeMatch.ScopeValue);
    }

    private async Task<List<RoleGrant>> ResolveUserRoleGrantsAsync(int userId)
    {
        var direct = await (
            from userRole in _context.UserRoles
            where userRole.UserId == userId
            join role in _context.Roles on userRole.RoleId equals role.Id
            select new RoleGrant(role.Id, role.Name!, "Global", null)
        ).ToListAsync();

        var now = DateTime.Now;
        var assigned = await (
            from assignment in _context.RoleAssignments
            where assignment.UserId == userId && assignment.IsActive
                && (assignment.EffectiveFrom == null || assignment.EffectiveFrom <= now)
                && (assignment.EffectiveTo == null || assignment.EffectiveTo >= now)
            join role in _context.Roles on assignment.RoleId equals role.Id
            select new RoleGrant(role.Id, role.Name!, assignment.ScopeType, assignment.ScopeValue)
        ).ToListAsync();

        return direct.Concat(assigned).ToList();
    }

    private async Task<AccessDecision> DecideAsync(List<string> roleNames, string resourceName, string actionName)
    {
        // طبق بند ۱۲ سند PEMS-SEC-PERMIT-AUTH-001: یک نقش SystemAdmin عادی نباید به‌طور خودکار
        // اختیار Break-Glass داشته باشد - حتی SystemAdmin باید یک RolePermissionGrant صریح داشته باشد
        var isBreakGlassAction = resourceName == "ExecutionGate" && actionName == "EmergencyOverride";

        if (!isBreakGlassAction && roleNames.Contains(SystemRoles.SystemAdmin))
            return AccessDecision.Allow(SystemRoles.SystemAdmin, "SystemAdmin bypass");

        var permission = await _context.Permissions
            .FirstOrDefaultAsync(p => p.ResourceName == resourceName && p.ActionName == actionName && p.IsActive);

        if (permission == null)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, $"مجوزی برای «{resourceName}.{actionName}» تعریف نشده است.");

        if (roleNames.Count == 0)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, "کاربر هیچ نقشی ندارد.");

        var roleIds = await _context.Roles
            .Where(r => r.Name != null && roleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync();

        if (roleIds.Count == 0)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, "هیچ‌کدام از نقش‌های کاربر شناخته‌شده نیست.");

        var grantedRoleId = await _context.RolePermissionGrants
            .Where(g => g.IsActive && g.PermissionId == permission.PermissionId && roleIds.Contains(g.RoleId))
            .Select(g => (int?)g.RoleId)
            .FirstOrDefaultAsync();

        if (grantedRoleId == null)
            return AccessDecision.Deny(AccessDecisionReason.DeniedByRole, $"هیچ‌کدام از نقش‌های کاربر مجوز «{permission.Name}» ندارند.");

        var matchedRoleName = await _context.Roles.Where(r => r.Id == grantedRoleId).Select(r => r.Name).FirstOrDefaultAsync();
        return AccessDecision.Allow(matchedRoleName, $"از طریق نقش «{matchedRoleName}»");
    }
}
