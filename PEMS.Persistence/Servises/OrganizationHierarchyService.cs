using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>مدیریت سلسله‌مراتب سمت‌ها و تخصیص‌ها. خطاها به‌صورت کلید resx (Org_Err_*) برگردانده می‌شوند، نه Exception.</summary>
public class OrganizationHierarchyService : IOrganizationHierarchyService
{
    private readonly PEMSDbContext _ctx;
    private readonly IOrganizationValidationService _validator;

    public OrganizationHierarchyService(PEMSDbContext ctx, IOrganizationValidationService validator)
    {
        _ctx = ctx;
        _validator = validator;
    }

    public async Task<List<OrganizationPositionRow>> GetPositionRowsAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var positions = await _ctx.Positions.AsNoTracking().Include(p => p.Department).OrderBy(p => p.Department!.Name).ThenBy(p => p.SortOrder).ThenBy(p => p.Title).ToListAsync(ct);
        var byId = positions.ToDictionary(p => p.PositionId);
        var assigns = await _ctx.EmployeeAssignments.AsNoTracking().Include(a => a.Employee).Where(a => a.IsActive).ToListAsync(ct);
        var children = positions.Where(p => p.ParentPositionId.HasValue).GroupBy(p => p.ParentPositionId!.Value).ToDictionary(g => g.Key, g => g.Count());
        string Label(Position p) => string.IsNullOrWhiteSpace(p.PositionCode) ? p.Title : $"{p.Title} [{p.PositionCode}]";

        return positions.Select(p =>
        {
            var a = assigns.Where(x => x.PositionId == p.PositionId).OrderByDescending(x => OrganizationValidationService.IsEffective(x, now)).ThenBy(x => x.EmployeeAssignmentId).FirstOrDefault();
            return new OrganizationPositionRow
            {
                PositionId = p.PositionId, PositionCode = p.PositionCode, Title = p.Title,
                DepartmentId = p.DepartmentId, DepartmentName = p.Department?.Name,
                ParentPositionId = p.ParentPositionId,
                ParentLabel = p.ParentPositionId.HasValue && byId.TryGetValue(p.ParentPositionId.Value, out var par) ? Label(par) : null,
                IsActive = p.IsActive,
                AssignmentId = a?.EmployeeAssignmentId, HolderEmployeeId = a?.EmployeeId,
                HolderName = a?.Employee == null ? null : (a.Employee.FirstName + " " + a.Employee.LastName).Trim(),
                HolderIsPrimary = a?.IsPrimary ?? false, StartDate = a?.StartDate, EndDate = a?.EndDate,
                ChildCount = children.GetValueOrDefault(p.PositionId)
            };
        }).ToList();
    }

    public async Task<OrganizationChartDto> GetChartAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var positions = await _ctx.Positions.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new
            {
                p.PositionId, p.PositionCode, p.Title, p.ParentPositionId, p.SortOrder,
                Department = p.Department != null ? p.Department.Name : null,
                Team = p.Team != null ? p.Team.Name : null
            }).ToListAsync(ct);
        var ids = positions.Select(p => p.PositionId).ToHashSet();

        // دارنده‌ی جاری هر سمت (اصلی‌ها مقدم؛ ترتیب قطعی)
        var holders = (await (from a in _ctx.EmployeeAssignments.AsNoTracking()
                              join e in _ctx.Employees.AsNoTracking() on a.EmployeeId equals e.EmployeeId
                              where a.IsActive && e.IsActive
                              select new { a.PositionId, a.EmployeeAssignmentId, a.EmployeeId, a.IsPrimary, a.StartDate, a.EndDate, e.FirstName, e.LastName }).ToListAsync(ct))
            .Where(a => a.StartDate <= now && (!a.EndDate.HasValue || a.EndDate.Value >= now))
            .GroupBy(a => a.PositionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.EmployeeAssignmentId).First());

        var issuesByPosition = (await _validator.ValidateAllAsync(ct))
            .Where(i => i.PositionId.HasValue && i.Severity != "P3")
            .GroupBy(i => i.PositionId!.Value).ToDictionary(g => g.Key, g => g.Select(i => i.Code).Distinct().ToArray());

        // دورها: عضو دور = با پیمایش والدها به خودش برمی‌گردد
        var parentOf = positions.ToDictionary(p => p.PositionId, p => p.ParentPositionId);
        var inCycle = new HashSet<int>();
        foreach (var start in ids)
        {
            var seen = new HashSet<int> { start };
            var cur = parentOf[start];
            while (cur.HasValue && ids.Contains(cur.Value))
            {
                if (cur.Value == start) { inCycle.Add(start); break; }
                if (!seen.Add(cur.Value)) break;
                cur = parentOf[cur.Value];
            }
        }

        var childCount = positions.Where(p => p.ParentPositionId.HasValue && ids.Contains(p.ParentPositionId.Value) && p.ParentPositionId != p.PositionId)
            .GroupBy(p => p.ParentPositionId!.Value).ToDictionary(g => g.Key, g => g.Count());

        string KindOf(int id, int? parent)
        {
            if (inCycle.Contains(id) || parent == id) return "cycle";
            if (!parent.HasValue) return "root";
            return ids.Contains(parent.Value) ? "position" : "orphan";    // والد ناموجود/غیرفعال: ریشه‌ی علامت‌دار، نه مدیر ساختگی
        }

        var nodes = positions.OrderBy(p => p.SortOrder).ThenBy(p => p.Title).Select(p =>
        {
            var kind = KindOf(p.PositionId, p.ParentPositionId);
            holders.TryGetValue(p.PositionId, out var h);
            return new OrganizationChartNodeDto
            {
                Id = "p" + p.PositionId, ParentId = kind == "position" ? "p" + p.ParentPositionId : "",
                PositionId = p.PositionId, PositionCode = p.PositionCode, PositionTitle = p.Title,
                EmployeeId = h?.EmployeeId, EmployeeName = h == null ? null : (h.FirstName + " " + h.LastName).Trim(),
                DepartmentName = p.Department, TeamName = p.Team, IsVacant = h == null, HolderIsPrimary = h?.IsPrimary ?? false,
                AssignmentFrom = h?.StartDate.ToString("yyyy-MM-dd"), AssignmentTo = h?.EndDate?.ToString("yyyy-MM-dd"),
                NodeKind = kind, ChildCount = childCount.GetValueOrDefault(p.PositionId),
                IssueCodes = issuesByPosition.GetValueOrDefault(p.PositionId) ?? Array.Empty<string>()
            };
        }).ToList();

        var roots = nodes.Where(n => n.ParentId.Length == 0).ToList();
        var warnings = new List<OrganizationChartWarning>();
        var orphans = nodes.Count(n => n.NodeKind == "orphan"); if (orphans > 0) warnings.Add(new("ORPHAN_POSITIONS", orphans));
        var cycles = nodes.Count(n => n.NodeKind == "cycle"); if (cycles > 0) warnings.Add(new("HIERARCHY_CYCLE", cycles));

        // چند ریشه: فقط یک ظرف بصریِ بدون معنای سازمانی (سمت/مدیر نیست) تا کتابخانه (stratify) یک ریشه ببیند
        if (roots.Count > 1)
        {
            var wrapped = nodes.Select(n => n.ParentId.Length == 0
                ? new OrganizationChartNodeDto
                {
                    Id = n.Id, ParentId = "org-root", PositionId = n.PositionId, PositionCode = n.PositionCode, PositionTitle = n.PositionTitle,
                    EmployeeId = n.EmployeeId, EmployeeName = n.EmployeeName, DepartmentName = n.DepartmentName, TeamName = n.TeamName,
                    IsVacant = n.IsVacant, HolderIsPrimary = n.HolderIsPrimary, AssignmentFrom = n.AssignmentFrom, AssignmentTo = n.AssignmentTo,
                    NodeKind = n.NodeKind, ChildCount = n.ChildCount, IssueCodes = n.IssueCodes
                } : n).ToList();
            wrapped.Insert(0, new OrganizationChartNodeDto { Id = "org-root", ParentId = "", PositionTitle = "", NodeKind = "container", ChildCount = roots.Count, IsVacant = false });
            nodes = wrapped;
        }

        return new OrganizationChartDto { Nodes = nodes, Warnings = warnings, RootCount = roots.Count };
    }

    public async Task<OrganizationReadiness> GetReadinessAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var positions = await _ctx.Positions.AsNoTracking().Where(p => p.IsActive).Select(p => new { p.PositionId, p.ParentPositionId, p.DepartmentId }).ToListAsync(ct);
        var effective = (await _ctx.EmployeeAssignments.AsNoTracking().Where(a => a.IsActive).ToListAsync(ct)).Where(a => OrganizationValidationService.IsEffective(a, now)).ToList();
        var held = effective.Select(a => a.PositionId).ToHashSet();
        var primaryEmployees = effective.Where(a => a.IsPrimary).Select(a => a.EmployeeId).ToHashSet();
        var employees = await _ctx.Employees.AsNoTracking().Where(e => e.IsActive).Select(e => new { e.EmployeeId, e.PersonnelCode }).ToListAsync(ct);
        var userEmployees = (await _ctx.Users.AsNoTracking().Where(u => u.IsActive && u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToListAsync(ct)).ToHashSet();
        return new OrganizationReadiness(
            positions.Count, positions.Count(p => p.ParentPositionId.HasValue), positions.Count(p => !p.ParentPositionId.HasValue),
            positions.Count(p => !held.Contains(p.PositionId)), positions.Count(p => !p.DepartmentId.HasValue),
            employees.Count, employees.Count(e => primaryEmployees.Contains(e.EmployeeId)), employees.Count(e => string.IsNullOrWhiteSpace(e.PersonnelCode)),
            userEmployees.Count(id => employees.Any(e => e.EmployeeId == id) && !primaryEmployees.Contains(id)));
    }

    public async Task<DirectManagerInfo> GetDirectManagerAsync(int employeeId, CancellationToken ct = default)
    {
        var chain = await OrganizationManagerChain.WalkAsync(_ctx, employeeId, 1, DateTime.Now, ct);
        string? posLabel = null;
        if (chain.RequesterPositionId.HasValue)
            posLabel = await _ctx.Positions.AsNoTracking().Where(p => p.PositionId == chain.RequesterPositionId).Select(p => p.Title).FirstOrDefaultAsync(ct);
        var managers = new List<(int, string)>();
        if (chain.IssueCode == null && !chain.NoSuperior)
        {
            var emps = await _ctx.Employees.AsNoTracking().Where(e => chain.HolderEmployeeIds.Contains(e.EmployeeId))
                .Select(e => new { e.EmployeeId, e.FirstName, e.LastName }).ToListAsync(ct);
            managers = emps.Select(e => (e.EmployeeId, (e.FirstName + " " + e.LastName).Trim())).ToList();
        }
        return new DirectManagerInfo(posLabel, managers, chain.IssueCode, chain.Path);
    }

    public async Task<List<OrganizationBlockedRequest>> GetBlockedRequestsAsync(CancellationToken ct = default)
    {
        var rows = await (from r in _ctx.PartCatalogueRequests.AsNoTracking()
                          where r.IsActive && r.Status == PartCatalogueStatuses.OrganizationResolutionRequired
                          join e in _ctx.Employees.AsNoTracking() on r.RequesterEmployeeId equals e.EmployeeId
                          orderby r.SubmittedDate
                          select new { r.PartCatalogueRequestId, r.RequestNumber, e.FirstName, e.LastName, r.RequesterEmployeeId, r.RoutingBlockReason, r.SubmittedDate })
            .ToListAsync(ct);
        return rows.Select(x => new OrganizationBlockedRequest("PartCatalogue", x.PartCatalogueRequestId, x.RequestNumber,
            (x.FirstName + " " + x.LastName).Trim(), x.RequesterEmployeeId, x.RoutingBlockReason, x.SubmittedDate)).ToList();
    }

    public async Task<List<(int EmployeeId, string Label)>> GetActiveEmployeesAsync(CancellationToken ct = default)
    {
        var list = await _ctx.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Select(e => new { e.EmployeeId, e.FirstName, e.LastName, e.PersonnelCode }).ToListAsync(ct);
        return list.Select(e => (e.EmployeeId, ((e.FirstName + " " + e.LastName).Trim() + (string.IsNullOrEmpty(e.PersonnelCode) ? "" : " · " + e.PersonnelCode)))).ToList();
    }

    public async Task<OrgOpResult> SetPositionHierarchyAsync(int positionId, string? positionCode, int? parentPositionId, CancellationToken ct = default)
    {
        var pos = await _ctx.Positions.FirstOrDefaultAsync(p => p.PositionId == positionId, ct);
        if (pos == null) return OrgOpResult.Fail("Org_Err_PositionNotFound");

        var code = string.IsNullOrWhiteSpace(positionCode) ? null : positionCode.Trim();
        if (code != null && code.Length > 50) return OrgOpResult.Fail("Org_Err_CodeTooLong");
        if (code != null && await _ctx.Positions.AnyAsync(p => p.PositionCode == code && p.PositionId != positionId, ct))
            return OrgOpResult.Fail("Org_Err_DuplicatePositionCode", code);

        var issue = await _validator.ValidateParentChangeAsync(positionId, parentPositionId, ct);
        if (issue != null) return OrgOpResult.Fail("Org_Issue_" + issue);

        pos.PositionCode = code;
        pos.ParentPositionId = parentPositionId;
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return OrgOpResult.Fail("Org_Err_Concurrency"); }
        return OrgOpResult.Ok();
    }

    public async Task<OrgOpResult> AssignAsync(OrganizationAssignmentRequest r, CancellationToken ct = default)
    {
        var pos = await _ctx.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.PositionId == r.PositionId, ct);
        if (pos == null) return OrgOpResult.Fail("Org_Err_PositionNotFound");
        if (!pos.IsActive) return OrgOpResult.Fail("Org_Err_PositionInactive");
        var emp = await _ctx.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == r.EmployeeId, ct);
        if (emp == null) return OrgOpResult.Fail("Org_Err_EmployeeNotFound");
        if (!emp.IsActive) return OrgOpResult.Fail("Org_Err_EmployeeInactive");
        if (r.EndDate.HasValue && r.EndDate.Value < r.StartDate) return OrgOpResult.Fail("Org_Err_InvalidDates");
        if (await _ctx.EmployeeAssignments.AnyAsync(a => a.PositionId == r.PositionId && a.IsActive, ct)) return OrgOpResult.Fail("Org_Err_PositionOccupied");
        if (r.IsPrimary && await _ctx.EmployeeAssignments.AnyAsync(a => a.EmployeeId == r.EmployeeId && a.IsActive && a.IsPrimary, ct))
            return OrgOpResult.Fail("Org_Err_DuplicatePrimary");

        _ctx.EmployeeAssignments.Add(new EmployeeAssignment
        {
            PositionId = r.PositionId, EmployeeId = r.EmployeeId, IsActive = true, IsPrimary = r.IsPrimary,
            AssignmentType = r.IsPrimary ? EmployeeAssignmentTypes.Primary : EmployeeAssignmentTypes.Secondary,
            StartDate = r.StartDate, EndDate = r.EndDate, Notes = r.Notes, CreatedDate = DateTime.Now
        });
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return OrgOpResult.Fail("Org_Err_DuplicatePrimary"); }
        return OrgOpResult.Ok();
    }

    public async Task<OrgOpResult> UpdateAssignmentAsync(int assignmentId, bool isPrimary, DateTime startDate, DateTime? endDate, CancellationToken ct = default)
    {
        var a = await _ctx.EmployeeAssignments.FirstOrDefaultAsync(x => x.EmployeeAssignmentId == assignmentId, ct);
        if (a == null) return OrgOpResult.Fail("Org_Err_AssignmentNotFound");
        if (!a.IsActive) return OrgOpResult.Fail("Org_Err_AssignmentClosed");
        if (endDate.HasValue && endDate.Value < startDate) return OrgOpResult.Fail("Org_Err_InvalidDates");
        if (isPrimary && !a.IsPrimary && await _ctx.EmployeeAssignments.AnyAsync(x => x.EmployeeId == a.EmployeeId && x.IsActive && x.IsPrimary && x.EmployeeAssignmentId != assignmentId, ct))
            return OrgOpResult.Fail("Org_Err_DuplicatePrimary");

        a.IsPrimary = isPrimary;
        a.AssignmentType = isPrimary ? EmployeeAssignmentTypes.Primary : EmployeeAssignmentTypes.Secondary;
        a.StartDate = startDate;
        a.EndDate = endDate;
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return OrgOpResult.Fail("Org_Err_DuplicatePrimary"); }
        return OrgOpResult.Ok();
    }

    public async Task<OrgOpResult> EndAssignmentAsync(int assignmentId, string? notes, CancellationToken ct = default)
    {
        var a = await _ctx.EmployeeAssignments.FirstOrDefaultAsync(x => x.EmployeeAssignmentId == assignmentId, ct);
        if (a == null) return OrgOpResult.Fail("Org_Err_AssignmentNotFound");
        if (!a.IsActive) return OrgOpResult.Fail("Org_Err_AssignmentClosed");
        a.IsActive = false;
        a.EndDate = DateTime.Now;
        a.Notes = notes ?? a.Notes;
        await _ctx.SaveChangesAsync(ct);
        return OrgOpResult.Ok();
    }
}
