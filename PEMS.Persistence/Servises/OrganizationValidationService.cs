using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// اعتبارسنجی مرکزی ساختار سازمانی (سمت‌ها، سلسله‌مراتب مدیریتی، تخصیص‌ها). فقط می‌خواند و گزارش می‌دهد؛ هرگز داده را اصلاح نمی‌کند.
/// «تخصیص معتبر» = IsActive و StartDate ≤ اکنون و (EndDate خالی یا ≥ اکنون).
/// </summary>
public class OrganizationValidationService : IOrganizationValidationService
{
    private readonly PEMSDbContext _ctx;

    public OrganizationValidationService(PEMSDbContext ctx) => _ctx = ctx;

    public static bool IsEffective(EmployeeAssignment a, DateTime now) =>
        a.IsActive && a.StartDate <= now && (!a.EndDate.HasValue || a.EndDate.Value >= now);

    public async Task<string?> ValidateParentChangeAsync(int positionId, int? newParentPositionId, CancellationToken ct = default)
    {
        if (!newParentPositionId.HasValue) return null;
        if (newParentPositionId.Value == positionId) return OrganizationIssueCodes.SelfParent;

        var parents = await _ctx.Positions.AsNoTracking().Select(p => new { p.PositionId, p.ParentPositionId, p.IsActive }).ToDictionaryAsync(p => p.PositionId, ct);
        if (!parents.TryGetValue(newParentPositionId.Value, out var parent) || !parent.IsActive)
            return OrganizationIssueCodes.ParentPositionInactive;

        // آیا زنجیره‌ی مافوقِ والد جدید به خود سمت برمی‌گردد؟
        var seen = new HashSet<int> { positionId };
        var cur = (int?)newParentPositionId.Value;
        while (cur.HasValue)
        {
            if (!seen.Add(cur.Value)) return OrganizationIssueCodes.HierarchyCycle;
            cur = parents.TryGetValue(cur.Value, out var p) ? p.ParentPositionId : null;
        }
        return null;
    }

    public async Task<IReadOnlyList<OrganizationIssue>> ValidateAllAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var issues = new List<OrganizationIssue>();
        void Add(string sev, string code, string subject, int? pos, int? emp, string msg) => issues.Add(new OrganizationIssue(sev, code, subject, pos, emp, msg));

        var positions = await _ctx.Positions.AsNoTracking().ToListAsync(ct);
        var byId = positions.ToDictionary(p => p.PositionId);
        var depts = await _ctx.Departments.AsNoTracking().Select(d => new { d.DepartmentId, d.IsActive }).ToDictionaryAsync(d => d.DepartmentId, d => d.IsActive, ct);
        var assignments = await _ctx.EmployeeAssignments.AsNoTracking().Where(a => a.IsActive).ToListAsync(ct);
        var employees = await _ctx.Employees.AsNoTracking().Select(e => new { e.EmployeeId, e.FirstName, e.LastName, e.IsActive }).ToDictionaryAsync(e => e.EmployeeId, ct);
        string EmpName(int id) => employees.TryGetValue(id, out var e) ? (e.FirstName + " " + e.LastName).Trim() : "#" + id;
        string PosName(Position p) => string.IsNullOrWhiteSpace(p.PositionCode) ? p.Title : $"{p.Title} ({p.PositionCode})";

        var holders = assignments.Where(a => IsEffective(a, now)).GroupBy(a => a.PositionId).ToDictionary(g => g.Key, g => g.ToList());

        // ---- سمت‌ها / سلسله‌مراتب
        var reportedCycle = new HashSet<int>();
        foreach (var p in positions)
        {
            if (p.ParentPositionId == p.PositionId)
            { Add("P1", OrganizationIssueCodes.SelfParent, PosName(p), p.PositionId, null, "Position is its own parent."); continue; }

            if (p.ParentPositionId.HasValue)
            {
                // دور
                var seen = new HashSet<int> { p.PositionId };
                var cur = p.ParentPositionId;
                while (cur.HasValue && byId.ContainsKey(cur.Value))
                {
                    if (!seen.Add(cur.Value))
                    {
                        if (reportedCycle.Add(p.PositionId))
                            Add("P1", OrganizationIssueCodes.HierarchyCycle, PosName(p), p.PositionId, null, "The managerial chain above this position loops back on itself.");
                        break;
                    }
                    cur = byId[cur.Value].ParentPositionId;
                }

                if (p.IsActive && byId.TryGetValue(p.ParentPositionId.Value, out var parent))
                {
                    if (!parent.IsActive)
                        Add("P1", OrganizationIssueCodes.ParentPositionInactive, PosName(p), p.PositionId, null, $"Parent position '{PosName(parent)}' is inactive.");
                    else if (!holders.ContainsKey(parent.PositionId))
                        Add("P2", OrganizationIssueCodes.ParentPositionVacant, PosName(p), p.PositionId, null, $"Parent position '{PosName(parent)}' is vacant.");
                }
            }

            if (p.IsActive && !p.DepartmentId.HasValue && !p.TeamId.HasValue)
                Add("P3", OrganizationIssueCodes.PositionWithoutDepartment, PosName(p), p.PositionId, null, "Position has neither a department nor a team.");
            if (p.IsActive && p.DepartmentId.HasValue && depts.TryGetValue(p.DepartmentId.Value, out var dActive) && !dActive)
                Add("P2", OrganizationIssueCodes.DepartmentInactive, PosName(p), p.PositionId, null, "The position's department is inactive.");
        }

        // ---- تخصیص‌ها
        foreach (var a in assignments)
        {
            if (!byId.TryGetValue(a.PositionId, out var pos)) continue;
            if (a.EndDate.HasValue && a.EndDate.Value < now)
                Add("P2", OrganizationIssueCodes.AssignmentExpired, PosName(pos), a.PositionId, a.EmployeeId, $"Active assignment of {EmpName(a.EmployeeId)} ended on {a.EndDate:yyyy-MM-dd}.");
            else if (a.StartDate > now)
                Add("P3", OrganizationIssueCodes.AssignmentNotYetEffective, PosName(pos), a.PositionId, a.EmployeeId, $"Assignment of {EmpName(a.EmployeeId)} starts on {a.StartDate:yyyy-MM-dd}.");
            else
            {
                if (employees.TryGetValue(a.EmployeeId, out var e) && !e.IsActive)
                    Add("P1", OrganizationIssueCodes.HolderEmployeeInactive, PosName(pos), a.PositionId, a.EmployeeId, $"Holder {EmpName(a.EmployeeId)} is inactive.");
                if (a.IsPrimary && !pos.IsActive)
                    Add("P1", OrganizationIssueCodes.PrimaryPositionInactive, PosName(pos), a.PositionId, a.EmployeeId, $"Primary position of {EmpName(a.EmployeeId)} is inactive.");
            }
        }

        // ---- کارمندان: سمت اصلی (فقط کارمندان فعالِ دارای حساب کاربری = درخواست‌دهنده‌های بالقوه)
        var withAccount = (await _ctx.Users.AsNoTracking().Where(u => u.EmployeeId != null && u.IsActive).Select(u => u.EmployeeId!.Value).ToListAsync(ct)).ToHashSet();
        var primariesByEmp = assignments.Where(a => IsEffective(a, now) && a.IsPrimary).GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var kv in primariesByEmp.Where(kv => kv.Value > 1))
            Add("P1", OrganizationIssueCodes.MultiplePrimaryPositions, EmpName(kv.Key), null, kv.Key, "Employee has more than one effective primary position.");
        foreach (var empId in withAccount.Where(id => employees.TryGetValue(id, out var e) && e.IsActive && !primariesByEmp.ContainsKey(id)))
            Add("P2", OrganizationIssueCodes.EmployeeNoPrimaryPosition, EmpName(empId), null, empId, "Active user has no effective primary position; their direct manager cannot be resolved.");

        foreach (var kv in holders.Where(kv => kv.Value.Select(a => a.EmployeeId).Distinct().Count() > 1))
            if (byId.TryGetValue(kv.Key, out var multi))
                Add("P1", OrganizationIssueCodes.MultipleActiveHolders, PosName(multi), kv.Key, null, "The position has more than one effective holder.");

        // ---- اطلاعاتی: ریشه‌ها (مدیر مستقیم ندارند؛ معتبر است)
        foreach (var root in positions.Where(p => p.IsActive && !p.ParentPositionId.HasValue && holders.ContainsKey(p.PositionId)))
            Add("P3", OrganizationIssueCodes.RootPosition, PosName(root), root.PositionId, null, "Root position: valid, but its holder has no Direct Manager (requests from this position require organization resolution).");

        return issues.OrderBy(i => i.Severity).ThenBy(i => i.Code).ThenBy(i => i.Subject).ToList();
    }
}
