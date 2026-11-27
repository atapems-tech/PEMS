using PEMS.Web.Security;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-832: the data of the executive dashboard (see <see cref="IExecutiveDashboardOperations"/>); the same rules as the old ExecutiveDashboardController.Data.</summary>
public sealed class ExecutiveDashboardOperations(IExecutiveDashboardService dashboard, ICurrentUserService currentUser, IPermissionService permissions, IAccessControlService accessControl, PEMSDbContext context) : IExecutiveDashboardOperations
{
    public async Task<ExecutiveDashboardDto> DataAsync(int? projectId = null)
    {
        var roles = currentUser.Roles;
        var request = new ExecutiveDashboardRequest
        {
            ProjectId = projectId,
            CanViewEngineering = await permissions.HasPermissionAsync(roles, "EngineeringDocuments", "View"),
            CanViewVdr = await permissions.HasPermissionAsync(roles, "VDR", "View"),
            CanViewProcurement = await permissions.HasPermissionAsync(roles, "MaterialRequisition", "View")
                                 || await permissions.HasPermissionAsync(roles, "ProcurementPackage", "View"),
            CanViewCmms = await permissions.HasPermissionAsync(roles, "WorkOrder", "View"),
            CanViewCorrespondence = await permissions.HasPermissionAsync(roles, "Correspondence", "View")
        };

        // the rule of the engineering document list: without ViewAllDisciplines only the discipline of the employee (and documents without a discipline)
        var canViewAllDisciplines = (await accessControl.AuthorizeForRolesAsync(roles, "EngineeringDocuments", "ViewAllDisciplines")).Allowed;
        if (!canViewAllDisciplines)
        {
            request.RestrictToOwnDiscipline = true;
            if (currentUser.UserId.HasValue)
            {
                var userId = currentUser.UserId.Value;
                request.OwnDisciplineId = await context.Users.Where(u => u.Id == userId && u.EmployeeId != null)
                    .Join(context.Employees, u => u.EmployeeId, e => (int?)e.EmployeeId, (u, e) => (int?)e.DisciplineId)
                    .FirstOrDefaultAsync();
            }
        }

        return await dashboard.GetAsync(request);
    }
}
