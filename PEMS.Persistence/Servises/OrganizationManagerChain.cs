using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// تنها پیاده‌سازی «درخواست‌دهنده ← سمت اصلیِ معتبر ← ParentPosition (level بار) ← دارنده‌ی معتبر». هم Enterprise Actor Resolver
/// و هم صفحه‌ی نمایش مدیر مستقیم از همین استفاده می‌کنند؛ هیچ منطق مشابهی جای دیگر نیست. فقط می‌خواند.
/// بدون میان‌بر مدیرعامل/Admin/Department/Team Supervisor/اولین مدیر موجود.
/// </summary>
public static class OrganizationManagerChain
{
    public static async Task<ManagerChainResult> WalkAsync(PEMSDbContext ctx, int employeeId, int level, DateTime now, CancellationToken ct = default)
    {
        var result = new ManagerChainResult();
        ManagerChainResult Fail(string code, string detail)
        {
            result.IssueCode = code; result.Detail = detail; result.Path.Add("✗ " + code); return result;
        }

        var positions = (await ctx.Positions.AsNoTracking()
                .Select(p => new { p.PositionId, p.Title, p.PositionCode, p.ParentPositionId, p.IsActive }).ToListAsync(ct))
            .ToDictionary(p => p.PositionId);
        string PName(int id) => positions.TryGetValue(id, out var p) ? (string.IsNullOrWhiteSpace(p.PositionCode) ? p.Title : $"{p.Title} [{p.PositionCode}]") : $"#{id}";

        var emp = await ctx.Employees.AsNoTracking().Where(e => e.EmployeeId == employeeId).Select(e => new { e.FirstName, e.LastName }).FirstOrDefaultAsync(ct);
        result.Path.Add("Employee: " + (emp == null ? "#" + employeeId : (emp.FirstName + " " + emp.LastName).Trim()));

        var primaries = (await ctx.EmployeeAssignments.AsNoTracking().Where(a => a.EmployeeId == employeeId && a.IsActive && a.IsPrimary).ToListAsync(ct))
            .Where(a => OrganizationValidationService.IsEffective(a, now)).ToList();
        if (primaries.Count == 0) return Fail(OrganizationIssueCodes.EmployeeNoPrimaryPosition, "The employee has no effective primary position.");
        if (primaries.Count > 1) return Fail(OrganizationIssueCodes.MultiplePrimaryPositions, "The employee has more than one effective primary position.");

        var current = primaries[0].PositionId;
        result.RequesterPositionId = current;
        if (!positions.TryGetValue(current, out var curPos) || !curPos.IsActive)
            return Fail(OrganizationIssueCodes.PrimaryPositionInactive, $"The primary position '{PName(current)}' is inactive.");
        result.Path.Add("Primary Position: " + PName(current));

        var visited = new HashSet<int> { current };
        for (var step = 1; step <= Math.Max(level, 1); step++)
        {
            var parentId = positions[current].ParentPositionId;
            if (!parentId.HasValue)
            {
                if (step == 1) return Fail(OrganizationIssueCodes.RootPosition, $"'{PName(current)}' is a root position without a parent; there is no Direct Manager.");
                result.NoSuperior = true;
                result.Path.Add("Superior: none (the direct manager position is the root)");
                return result;
            }
            if (parentId.Value == current) return Fail(OrganizationIssueCodes.SelfParent, $"'{PName(current)}' is its own parent.");
            if (!visited.Add(parentId.Value)) return Fail(OrganizationIssueCodes.HierarchyCycle, "The managerial hierarchy loops back on itself.");
            if (!positions.TryGetValue(parentId.Value, out var par) || !par.IsActive)
                return Fail(OrganizationIssueCodes.ParentPositionInactive, $"Parent position '{PName(parentId.Value)}' is inactive.");
            current = parentId.Value;
            result.Path.Add((step == 1 ? "Parent Position: " : $"Parent Position (level {step}): ") + PName(current));
        }
        result.ResolvedPositionId = current;

        var all = await ctx.EmployeeAssignments.AsNoTracking().Where(a => a.PositionId == current && a.IsActive).ToListAsync(ct);
        var holders = all.Where(a => OrganizationValidationService.IsEffective(a, now)).Select(a => a.EmployeeId).Distinct().ToList();
        if (holders.Count == 0)
            return Fail(all.Count == 0 ? OrganizationIssueCodes.ParentPositionVacant : OrganizationIssueCodes.AssignmentExpired,
                all.Count == 0 ? $"Position '{PName(current)}' is vacant." : $"The assignment to '{PName(current)}' is outside its validity period.");

        var info = await ctx.Employees.AsNoTracking().Where(e => holders.Contains(e.EmployeeId)).Select(e => new { e.EmployeeId, e.IsActive }).ToListAsync(ct);
        var active = holders.Where(h => info.Any(i => i.EmployeeId == h && i.IsActive)).ToList();
        if (active.Count == 0) return Fail(OrganizationIssueCodes.HolderEmployeeInactive, $"The holder of '{PName(current)}' is inactive.");
        if (active.All(h => h == employeeId)) return Fail(OrganizationIssueCodes.SelfManager, "The holder of the manager position is the employee themself.");

        result.HolderEmployeeIds.AddRange(active.Where(h => h != employeeId));
        result.Path.Add("Holder");
        return result;
    }
}
