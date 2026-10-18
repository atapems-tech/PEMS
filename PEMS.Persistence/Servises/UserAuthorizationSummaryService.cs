using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise User-Centric Authorization Workspace Consolidation»: پیاده‌سازی -
/// GetEffectivePermissionsForRolesAsync عیناً منطق منتقل‌شده از AuthorizationController.
/// BuildRoleRowsAsync خصوصی سابق است (رفتار باید کاملاً یکسان بماند - AuthorizationController اکنون
/// خودش هم از همین متد استفاده می‌کند). GetSoDExposureForRolesAsync ترکیب دو منبع موجود است
/// (SoDPolicies فعال + همین Effective Permissions) - نه یک موتور SoD تازه.
/// </summary>
public class UserAuthorizationSummaryService : IUserAuthorizationSummaryService
{
    private readonly PEMSDbContext _context;
    private readonly IAccessControlService _accessControlService;

    public UserAuthorizationSummaryService(PEMSDbContext context, IAccessControlService accessControlService)
    {
        _context = context;
        _accessControlService = accessControlService;
    }

    public async Task<List<EffectivePermissionDto>> GetEffectivePermissionsForRolesAsync(IEnumerable<string> roles)
    {
        var roleList = roles.ToArray();

        var permissions = await _context.Permissions
            .Where(p => p.IsActive)
            .OrderBy(p => p.ResourceName).ThenBy(p => p.ActionName)
            .ToListAsync();

        var rows = new List<EffectivePermissionDto>(permissions.Count);
        foreach (var p in permissions)
        {
            var decision = await _accessControlService.AuthorizeForRolesAsync(roleList, p.ResourceName, p.ActionName);
            rows.Add(new EffectivePermissionDto
            {
                ResourceName = p.ResourceName,
                ActionName = p.ActionName,
                Allowed = decision.Allowed,
                IsDestructive = p.IsDestructive,
                Detail = decision.Detail
            });
        }
        return rows;
    }

    public async Task<List<SoDExposureDto>> GetSoDExposureForRolesAsync(IEnumerable<string> roles)
    {
        var roleList = roles.ToArray();

        var activePolicies = await _context.SoDPolicies
            .Include(p => p.PermissionA)
            .Include(p => p.PermissionB)
            .Where(p => p.IsActive && p.PermissionA != null && p.PermissionB != null)
            .ToListAsync();

        if (activePolicies.Count == 0 || roleList.Length == 0)
            return new List<SoDExposureDto>();

        // طبق مستندسازی: این‌جا عمداً فقط دو طرف سیاست‌های فعال بررسی می‌شود (نه هر ۲۰۰+
        // Permission) - سبک‌تر از GetEffectivePermissionsForRolesAsync کامل، چون فقط چند مجوز
        // مشخص لازم است، نه کل کاتالوگ.
        var relevantPermissionIds = activePolicies
            .SelectMany(p => new[] { p.PermissionAId, p.PermissionBId })
            .Distinct()
            .ToList();

        var relevantPermissions = await _context.Permissions
            .Where(p => relevantPermissionIds.Contains(p.PermissionId))
            .ToListAsync();

        var allowedByPermissionId = new Dictionary<int, bool>();
        foreach (var permission in relevantPermissions)
        {
            var decision = await _accessControlService.AuthorizeForRolesAsync(roleList, permission.ResourceName, permission.ActionName);
            allowedByPermissionId[permission.PermissionId] = decision.Allowed;
        }

        return activePolicies.Select(policy => new SoDExposureDto
        {
            PermissionAName = policy.PermissionA!.Name,
            PermissionBName = policy.PermissionB!.Name,
            PolicyDescription = policy.Description,
            UserHoldsBoth = allowedByPermissionId.GetValueOrDefault(policy.PermissionAId) &&
                            allowedByPermissionId.GetValueOrDefault(policy.PermissionBId)
        }).ToList();
    }
}
