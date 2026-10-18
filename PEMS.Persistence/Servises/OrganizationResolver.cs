using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Persistence.Context;
using PEMS.Persistence.Workflow;

namespace PEMS.Persistence.Services;

/// <summary>
/// Thin facade (IOrganizationResolver). Delegates to the existing authoritative pieces; adds no organizational logic of its own:
///   manager      → OrganizationManagerChain
///   domain       → WorkOrderWorkflow.ResolveDepartmentDomainAsync (Department hierarchy)
///   primary pos. → EmployeeAssignment (active, primary, effective)
/// </summary>
public class OrganizationResolver : IOrganizationResolver
{
    private readonly PEMSDbContext _ctx;

    public OrganizationResolver(PEMSDbContext ctx) => _ctx = ctx;

    public Task<string?> ResolveDomainAsync(int departmentId, CancellationToken ct = default) =>
        WorkOrderWorkflow.ResolveDepartmentDomainAsync(_ctx, departmentId);

    public async Task<(string? Domain, int? DisciplineId)> GetDomainAndDisciplineAsync(int? employeeId, CancellationToken ct = default)
    {
        if (!employeeId.HasValue) return (null, null);
        var e = await _ctx.Employees.AsNoTracking().Where(x => x.EmployeeId == employeeId.Value)
            .Select(x => new { x.DepartmentId, x.DisciplineId }).FirstOrDefaultAsync(ct);
        if (e == null) return (null, null);
        return (await ResolveDomainAsync(e.DepartmentId, ct), e.DisciplineId);
    }

    public async Task<OrganizationalContext> GetContextAsync(int employeeId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var now = asOf ?? DateTime.Now;
        var e = await _ctx.Employees.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .Select(x => new { x.DepartmentId, x.DisciplineId, x.IsActive }).FirstOrDefaultAsync(ct);
        if (e == null) return new OrganizationalContext(employeeId, false, false, null, null, null, null, null, null, null);

        var primaries = (await _ctx.EmployeeAssignments.AsNoTracking().Where(a => a.EmployeeId == employeeId && a.IsActive && a.IsPrimary).ToListAsync(ct))
            .Where(a => OrganizationValidationService.IsEffective(a, now)).ToList();
        int? posId = primaries.Count == 1 ? primaries[0].PositionId : null;      // 0 or >1 primaries = ambiguous: no position is guessed
        string? title = null; int? posDept = null, parent = null;
        if (posId.HasValue)
        {
            var p = await _ctx.Positions.AsNoTracking().Where(x => x.PositionId == posId.Value)
                .Select(x => new { x.Title, x.DepartmentId, x.ParentPositionId }).FirstOrDefaultAsync(ct);
            title = p?.Title; posDept = p?.DepartmentId; parent = p?.ParentPositionId;
        }
        return new OrganizationalContext(employeeId, true, e.IsActive, e.DepartmentId, e.DisciplineId,
            await ResolveDomainAsync(e.DepartmentId, ct), posId, title, posDept, parent);
    }

    public Task<ManagerChainResult> ResolveManagerAsync(int employeeId, int level = 1, DateTime? asOf = null, CancellationToken ct = default) =>
        OrganizationManagerChain.WalkAsync(_ctx, employeeId, level, asOf ?? DateTime.Now, ct);

    public async Task<IReadOnlyList<int>> GetPositionHoldersAsync(int positionId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var now = asOf ?? DateTime.Now;
        var rows = await (from a in _ctx.EmployeeAssignments.AsNoTracking()
                          join e in _ctx.Employees.AsNoTracking() on a.EmployeeId equals e.EmployeeId
                          where a.PositionId == positionId && a.IsActive && e.IsActive
                          select a).ToListAsync(ct);
        return rows.Where(a => OrganizationValidationService.IsEffective(a, now)).Select(a => a.EmployeeId).Distinct().OrderBy(x => x).ToList();
    }
}
