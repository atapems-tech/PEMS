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
                PositionId = p.PositionId, PositionCode = p.PositionCode, Title = p.Title, Description = p.Description,
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

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private async Task<OrgOpResult?> ValidatePositionFieldsAsync(string title, string? code, int? departmentId, int? selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return OrgOpResult.Fail("Org_Err_TitleRequired");
        if (title.Trim().Length > 150) return OrgOpResult.Fail("Org_Err_TitleTooLong");
        var c = Clean(code);
        if (c != null && c.Length > 50) return OrgOpResult.Fail("Org_Err_CodeTooLong");
        if (c != null && await _ctx.Positions.AnyAsync(p => p.PositionCode == c && (selfId == null || p.PositionId != selfId), ct))
            return OrgOpResult.Fail("Org_Err_DuplicatePositionCode", c);
        if (departmentId.HasValue && !await _ctx.Departments.AnyAsync(d => d.DepartmentId == departmentId.Value && d.IsActive, ct))
            return OrgOpResult.Fail("Org_Err_DepartmentInvalid");
        return null;
    }

    public async Task<OrgOpResult> CreatePositionAsync(string title, string? positionCode, int? departmentId, string? description, int? parentPositionId, CancellationToken ct = default)
    {
        var invalid = await ValidatePositionFieldsAsync(title, positionCode, departmentId, null, ct);
        if (invalid != null) return invalid;
        if (parentPositionId.HasValue && !await _ctx.Positions.AnyAsync(p => p.PositionId == parentPositionId.Value && p.IsActive, ct))
            return OrgOpResult.Fail("Org_Issue_PARENT_POSITION_INACTIVE");

        _ctx.Positions.Add(new Position
        {
            Title = title.Trim(), PositionCode = Clean(positionCode), DepartmentId = departmentId, Description = Clean(description),
            ParentPositionId = parentPositionId, IsActive = true, CreatedDate = DateTime.Now
        });
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return OrgOpResult.Fail("Org_Err_DuplicatePositionCode"); }
        return OrgOpResult.Ok();
    }

    public async Task<OrgOpResult> UpdatePositionAsync(int positionId, string title, string? positionCode, int? departmentId, string? description, CancellationToken ct = default)
    {
        var pos = await _ctx.Positions.FirstOrDefaultAsync(p => p.PositionId == positionId, ct);
        if (pos == null) return OrgOpResult.Fail("Org_Err_PositionNotFound");
        // دپارتمان فعلیِ غیرفعال بدون تغییر مجاز است
        var dept = departmentId.HasValue && departmentId == pos.DepartmentId ? null : departmentId;
        var invalid = await ValidatePositionFieldsAsync(title, positionCode, dept, positionId, ct);
        if (invalid != null) return invalid;

        pos.Title = title.Trim(); pos.PositionCode = Clean(positionCode); pos.DepartmentId = departmentId; pos.Description = Clean(description);
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return OrgOpResult.Fail("Org_Err_Concurrency"); }
        catch (DbUpdateException) { return OrgOpResult.Fail("Org_Err_DuplicatePositionCode"); }
        return OrgOpResult.Ok();
    }

    public async Task<OrgOpResult> SetPositionActiveAsync(int positionId, bool isActive, CancellationToken ct = default)
    {
        var pos = await _ctx.Positions.FirstOrDefaultAsync(p => p.PositionId == positionId, ct);
        if (pos == null) return OrgOpResult.Fail("Org_Err_PositionNotFound");
        if (!isActive && await _ctx.EmployeeAssignments.AnyAsync(a => a.PositionId == positionId && a.IsActive, ct))
            return OrgOpResult.Fail("Org_Err_PositionHasHolder");
        pos.IsActive = isActive;
        await _ctx.SaveChangesAsync(ct);
        return OrgOpResult.Ok();
    }

    public async Task<List<(int DepartmentId, string Name)>> GetActiveDepartmentsAsync(CancellationToken ct = default) =>
        (await _ctx.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new { d.DepartmentId, d.Name }).ToListAsync(ct))
            .Select(d => (d.DepartmentId, d.Name)).ToList();

    private static string PosLabel(string title, string? code) => string.IsNullOrWhiteSpace(code) ? title : title + " [" + code + "]";

    public async Task<List<(int PositionId, string Label)>> GetVacantPositionsAsync(CancellationToken ct = default)
    {
        var occupied = _ctx.EmployeeAssignments.Where(a => a.IsActive).Select(a => a.PositionId);
        var rows = await _ctx.Positions.AsNoTracking().Where(p => p.IsActive && !occupied.Contains(p.PositionId))
            .OrderBy(p => p.Title).Select(p => new { p.PositionId, p.Title, p.PositionCode }).ToListAsync(ct);
        return rows.Select(p => (p.PositionId, PosLabel(p.Title, p.PositionCode))).ToList();
    }

    public async Task<Dictionary<int, (int PositionId, string Label)>> GetPrimaryPositionsAsync(IEnumerable<int> employeeIds, CancellationToken ct = default)
    {
        var ids = employeeIds.Distinct().ToList();
        var now = DateTime.Now;
        var rows = (await (from a in _ctx.EmployeeAssignments.AsNoTracking()
                           join p in _ctx.Positions.AsNoTracking() on a.PositionId equals p.PositionId
                           where a.IsActive && a.IsPrimary && ids.Contains(a.EmployeeId)
                           select new { a.EmployeeId, a.StartDate, a.EndDate, p.PositionId, p.Title, p.PositionCode }).ToListAsync(ct))
            .Where(a => a.StartDate <= now && (!a.EndDate.HasValue || a.EndDate.Value >= now));
        return rows.GroupBy(a => a.EmployeeId).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => (g.First().PositionId, PosLabel(g.First().Title, g.First().PositionCode)));
    }

    public async Task<OrgOpResult> SetPrimaryPositionAsync(int employeeId, int? positionId, CancellationToken ct = default)
    {
        var emp = await _ctx.Employees.AsNoTracking().Where(e => e.EmployeeId == employeeId).Select(e => new { e.IsActive }).FirstOrDefaultAsync(ct);
        if (emp == null) return OrgOpResult.Fail("Org_Err_EmployeeNotFound");

        var current = await _ctx.EmployeeAssignments.Where(a => a.EmployeeId == employeeId && a.IsActive && a.IsPrimary).ToListAsync(ct);
        if (positionId.HasValue && current.Count == 1 && current[0].PositionId == positionId.Value) return OrgOpResult.Ok();   // no change

        if (positionId.HasValue)
        {
            // پیش‌اعتبارسنجی تا سمت فعلی بدون جایگزین بسته نشود
            var pos = await _ctx.Positions.AsNoTracking().Where(p => p.PositionId == positionId.Value).Select(p => new { p.IsActive }).FirstOrDefaultAsync(ct);
            if (pos == null) return OrgOpResult.Fail("Org_Err_PositionNotFound");
            if (!pos.IsActive) return OrgOpResult.Fail("Org_Err_PositionInactive");
            if (!emp.IsActive) return OrgOpResult.Fail("Org_Err_EmployeeInactive");
            if (await _ctx.EmployeeAssignments.AnyAsync(a => a.PositionId == positionId.Value && a.IsActive, ct)) return OrgOpResult.Fail("Org_Err_PositionOccupied");
        }

        // پایان سمت قبلی + تخصیص سمت جدید یک واحد اتمیک است؛ اگر تخصیص شکست بخورد (مثلاً رقابت هم‌زمان)، کارمند بدون سمت اصلی نمی‌ماند
        // SqlServerRetryingExecutionStrategy تراکنش کاربر را فقط داخل strategy.ExecuteAsync می‌پذیرد
        var ownsTx = _ctx.Database.IsRelational() && _ctx.Database.CurrentTransaction == null;
        var strategy = _ctx.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var tx = ownsTx ? await _ctx.Database.BeginTransactionAsync(ct) : null;
            try
            {
                foreach (var a in current) { var ended = await EndAssignmentAsync(a.EmployeeAssignmentId, "Position changed", ct); if (!ended.Success) { if (tx != null) await tx.RollbackAsync(ct); return ended; } }
                if (!positionId.HasValue) { if (tx != null) await tx.CommitAsync(ct); return OrgOpResult.Ok(); }
                var assigned = await AssignAsync(new OrganizationAssignmentRequest(positionId.Value, employeeId, true, DateTime.Now, null, "Position assignment"), ct);
                if (tx != null) { if (assigned.Success) await tx.CommitAsync(ct); else { await tx.RollbackAsync(ct); _ctx.ChangeTracker.Clear(); } }
                return assigned;
            }
            catch { if (tx != null) await tx.RollbackAsync(ct); throw; }
            finally { if (tx != null) await tx.DisposeAsync(); }
        });
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
