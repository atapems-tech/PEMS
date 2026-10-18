using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;

namespace PEMS.Persistence.Seed;

/// <summary>
/// Phase 4 - مجوزهای WorkflowManagement.* (Idempotent) و Grant پیش‌فرض برای SystemAdmin تا رفتار فعلی تغییر نکند.
/// AccessControlService برای منبع WorkflowManagement حتی SystemAdmin را بدون Grant صریح رد می‌کند (هم‌الگو با Break-Glass).
/// </summary>
public static class WorkflowManagementSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        var admin = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.SystemAdmin);
        foreach (var action in WorkflowManagementPermissions.All)
        {
            var perm = await WorkOrderGovernanceSeeder.GetOrCreatePermissionAsync(db, WorkflowManagementPermissions.Resource + "." + action);
            if (admin == null) continue;
            var grant = await db.RolePermissionGrants.FirstOrDefaultAsync(g => g.RoleId == admin.Id && g.PermissionId == perm.PermissionId);
            if (grant == null)
            {
                db.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = admin.Id, PermissionId = perm.PermissionId, IsActive = true, CreatedDate = DateTime.Now });
                await db.SaveChangesAsync();
            }
        }
    }
}
