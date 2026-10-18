using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند «PEMS Phase 6»: پیاده‌سازی CRUD روی NavigationVisibilityOverride - عمداً بدون هیچ
/// وابستگی به IAccessControlService (Visibility هرگز تصمیم Authorization نمی‌گیرد و نمی‌خواند).
/// </summary>
public class NavigationVisibilityOverrideService : INavigationVisibilityOverrideService
{
    private readonly PEMSDbContext _context;

    public NavigationVisibilityOverrideService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<NavigationVisibilityOverrideDto>> GetOverridesForRolesAsync(IEnumerable<int> roleIds)
    {
        var ids = roleIds.Distinct().ToList();
        if (ids.Count == 0) return new List<NavigationVisibilityOverrideDto>();

        return await (
            from o in _context.NavigationVisibilityOverrides
            join r in _context.Roles on o.RoleId equals r.Id
            where ids.Contains(o.RoleId)
            select ToDto(o, r.Name ?? string.Empty)
        ).ToListAsync();
    }

    public async Task<List<NavigationVisibilityOverrideDto>> GetOverridesForRoleNamesAsync(IEnumerable<string> roleNames)
    {
        var names = roleNames.Distinct().ToList();
        if (names.Count == 0) return new List<NavigationVisibilityOverrideDto>();

        return await (
            from o in _context.NavigationVisibilityOverrides
            join r in _context.Roles on o.RoleId equals r.Id
            where r.Name != null && names.Contains(r.Name)
            select ToDto(o, r.Name ?? string.Empty)
        ).ToListAsync();
    }

    public async Task<List<NavigationVisibilityOverrideDto>> GetOverridesForRoleAsync(int roleId)
    {
        return await (
            from o in _context.NavigationVisibilityOverrides
            join r in _context.Roles on o.RoleId equals r.Id
            where o.RoleId == roleId
            select ToDto(o, r.Name ?? string.Empty)
        ).ToListAsync();
    }

    public async Task SetOverrideAsync(int roleId, string moduleKey, string? itemKey, bool isVisible, string? reason, string actingUser)
    {
        var existing = await _context.NavigationVisibilityOverrides.FirstOrDefaultAsync(o =>
            o.RoleId == roleId && o.ModuleKey == moduleKey && o.ItemKey == itemKey);

        if (existing == null)
        {
            _context.NavigationVisibilityOverrides.Add(new NavigationVisibilityOverride
            {
                RoleId = roleId,
                ModuleKey = moduleKey,
                ItemKey = itemKey,
                IsVisible = isVisible,
                Reason = reason,
                CreatedBy = actingUser,
                CreatedDate = DateTime.Now
            });
        }
        else
        {
            existing.IsVisible = isVisible;
            existing.Reason = reason;
            existing.ModifiedBy = actingUser;
            existing.ModifiedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ClearOverrideAsync(int roleId, string moduleKey, string? itemKey)
    {
        var existing = await _context.NavigationVisibilityOverrides.FirstOrDefaultAsync(o =>
            o.RoleId == roleId && o.ModuleKey == moduleKey && o.ItemKey == itemKey);

        if (existing != null)
        {
            _context.NavigationVisibilityOverrides.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }

    private static NavigationVisibilityOverrideDto ToDto(NavigationVisibilityOverride o, string roleName) => new()
    {
        NavigationVisibilityOverrideId = o.NavigationVisibilityOverrideId,
        RoleId = o.RoleId,
        RoleName = roleName,
        ModuleKey = o.ModuleKey,
        ItemKey = o.ItemKey,
        IsVisible = o.IsVisible,
        Reason = o.Reason,
        ModifiedBy = o.ModifiedBy ?? o.CreatedBy,
        ModifiedDate = o.ModifiedDate ?? o.CreatedDate
    };
}
