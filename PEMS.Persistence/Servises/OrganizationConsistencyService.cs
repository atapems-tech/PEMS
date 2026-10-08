using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>Read-only consistency diagnostics (Employee ↔ Position ↔ Department, legacy Employee.PositionId). Never repairs anything.</summary>
public class OrganizationConsistencyService : IOrganizationConsistencyService
{
    private readonly PEMSDbContext _ctx;

    public OrganizationConsistencyService(PEMSDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<EmployeeDepartmentConsistencyRow>> GetDepartmentConsistencyAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var depts = await _ctx.Departments.AsNoTracking().Select(d => new { d.DepartmentId, d.Name }).ToDictionaryAsync(d => d.DepartmentId, d => d.Name, ct);
        var positions = await _ctx.Positions.AsNoTracking().Select(p => new { p.PositionId, p.Title, p.DepartmentId }).ToDictionaryAsync(p => p.PositionId, ct);
        var employees = await _ctx.Employees.AsNoTracking().Where(e => e.IsActive)
            .Select(e => new { e.EmployeeId, e.FirstName, e.LastName, e.DepartmentId }).ToListAsync(ct);
        var primaries = (await _ctx.EmployeeAssignments.AsNoTracking().Where(a => a.IsActive && a.IsPrimary).ToListAsync(ct))
            .Where(a => OrganizationValidationService.IsEffective(a, now)).GroupBy(a => a.EmployeeId).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().PositionId);

        var rows = new List<EmployeeDepartmentConsistencyRow>();
        foreach (var e in employees)
        {
            if (!primaries.TryGetValue(e.EmployeeId, out var posId) || !positions.TryGetValue(posId, out var pos)) continue;
            int? empDept = e.DepartmentId > 0 && depts.ContainsKey(e.DepartmentId) ? e.DepartmentId : null;
            int? posDept = pos.DepartmentId.HasValue && depts.ContainsKey(pos.DepartmentId.Value) ? pos.DepartmentId : null;
            var status = (empDept, posDept) switch
            {
                (null, null) => DepartmentConsistencyStatuses.NoDepartment,
                (not null, null) => DepartmentConsistencyStatuses.EmployeeOnly,
                (null, not null) => DepartmentConsistencyStatuses.PositionOnly,
                _ => empDept == posDept ? DepartmentConsistencyStatuses.Consistent : DepartmentConsistencyStatuses.Conflict
            };
            rows.Add(new EmployeeDepartmentConsistencyRow(e.EmployeeId, (e.FirstName + " " + e.LastName).Trim(), empDept, empDept.HasValue ? depts[empDept.Value] : null,
                posId, pos.Title, posDept, posDept.HasValue ? depts[posDept.Value] : null, status));
        }
        return rows.OrderBy(r => r.Status == DepartmentConsistencyStatuses.Consistent).ThenBy(r => r.EmployeeName).ToList();
    }

    public async Task<IReadOnlyList<LegacyPositionRow>> GetLegacyPositionConflictsAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var legacy = await _ctx.Employees.AsNoTracking().Where(e => e.PositionId != null)
            .Select(e => new { e.EmployeeId, e.FirstName, e.LastName, PositionId = e.PositionId!.Value }).ToListAsync(ct);
        if (legacy.Count == 0) return Array.Empty<LegacyPositionRow>();
        var positions = await _ctx.Positions.AsNoTracking().Select(p => new { p.PositionId, p.IsActive }).ToDictionaryAsync(p => p.PositionId, p => p.IsActive, ct);
        var primaries = (await _ctx.EmployeeAssignments.AsNoTracking().Where(a => a.IsActive && a.IsPrimary).ToListAsync(ct))
            .Where(a => OrganizationValidationService.IsEffective(a, now)).GroupBy(a => a.EmployeeId).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().PositionId);

        return legacy.Select(e =>
        {
            primaries.TryGetValue(e.EmployeeId, out var prim);
            int? primary = primaries.ContainsKey(e.EmployeeId) ? prim : null;
            var status = !positions.TryGetValue(e.PositionId, out var active) || !active ? LegacyPositionStatuses.LegacyReferenceInvalid
                : primary == e.PositionId ? LegacyPositionStatuses.LegacyMatch
                : LegacyPositionStatuses.LegacyConflict;
            return new LegacyPositionRow(e.EmployeeId, (e.FirstName + " " + e.LastName).Trim(), e.PositionId, primary, status);
        }).OrderBy(r => r.Status == LegacyPositionStatuses.LegacyMatch).ThenBy(r => r.EmployeeName).ToList();
    }
}
