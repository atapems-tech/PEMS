using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق قرارداد AccessControlSeeder: هر ردیف AspNetUserRoles یک RoleAssignment(Direct, Global, بدون تاریخ) متناظر دارد («آینه»).
/// این سرویس همان قرارداد را در زمان تغییر نقش‌ها (و نه فقط در Startup) حفظ می‌کند. هیچ جدول/سیستم مجوز تازه‌ای نیست.
/// </summary>
public class UserRoleAssignmentSynchronizer : IUserRoleAssignmentSynchronizer
{
    public const string MirrorReason = "IdentityRoleMirror";
    private readonly PEMSDbContext _ctx;

    public UserRoleAssignmentSynchronizer(PEMSDbContext ctx) => _ctx = ctx;

    /// <summary>تخصیص «آینه‌ای»: Direct + Global + بدون ScopeValue + بدون بازه‌ی زمانی (Reason خالی = ساخته‌ی Seeder قدیمی، یا IdentityRoleMirror).</summary>
    private static bool IsMirror(RoleAssignment a) =>
        a.AssignmentType == "Direct" && a.ScopeType == "Global" && a.ScopeValue == null
        && a.EffectiveFrom == null && a.EffectiveTo == null && a.DelegatedByUserId == null
        && (a.Reason == null || a.Reason == MirrorReason);

    public async Task<int> SyncAsync(int userId, CancellationToken ct = default)
    {
        var roleIds = (await _ctx.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct)).ToHashSet();
        var assignments = await _ctx.RoleAssignments.Where(a => a.UserId == userId).ToListAsync(ct);
        var changes = 0;

        foreach (var a in assignments.Where(a => a.IsActive && IsMirror(a) && !roleIds.Contains(a.RoleId)))
        {
            a.IsActive = false;                     // غیرفعال‌سازی، نه حذف (تاریخچه حفظ می‌شود)
            a.EffectiveTo = DateTime.Now;
            a.Reason = MirrorReason;
            changes++;
        }

        foreach (var roleId in roleIds)
        {
            if (assignments.Any(a => a.RoleId == roleId && a.IsActive && IsMirror(a))) continue;
            var dormant = assignments.FirstOrDefault(a => a.RoleId == roleId && !a.IsActive && a.AssignmentType == "Direct" && a.ScopeType == "Global"
                                                          && a.ScopeValue == null && a.Reason == MirrorReason);
            if (dormant != null) { dormant.IsActive = true; dormant.EffectiveTo = null; }
            else _ctx.RoleAssignments.Add(new RoleAssignment { UserId = userId, RoleId = roleId, ScopeType = "Global", AssignmentType = "Direct", IsActive = true, Reason = MirrorReason, CreatedDate = DateTime.Now });
            changes++;
        }

        if (changes > 0) await _ctx.SaveChangesAsync(ct);
        return changes;
    }

    public async Task<IReadOnlyList<int>> FindStaleMirrorsAsync(CancellationToken ct = default)
    {
        var pairs = (await _ctx.UserRoles.AsNoTracking().Select(ur => new { ur.UserId, ur.RoleId }).ToListAsync(ct)).Select(x => (x.UserId, x.RoleId)).ToHashSet();
        return (await _ctx.RoleAssignments.AsNoTracking().Where(a => a.IsActive).ToListAsync(ct))
            .Where(a => IsMirror(a) && !pairs.Contains((a.UserId, a.RoleId))).Select(a => a.RoleAssignmentId).ToList();
    }
}
