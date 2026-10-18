using Microsoft.EntityFrameworkCore;
using PEMS.Application.ActorResolution;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Enterprise Actor Resolver (مشترک بین همه‌ی گردش‌کارها). خط لوله‌ی قطعی:
///   مسئولیت (پیکربندی) ← کاندیدهای پایه (Position/Role/Commodity/Manager/Pool) ←
///   حذف درخواست‌دهنده (SoD) ← کارمند فعال ← اعتبار تخصیص ← Scope سازمانی ← نقش ← مجوز ← راهبرد انتخاب.
/// هیچ‌جا از FirstOrDefault روی مجموعه‌ی نامرتب استفاده نمی‌شود؛ ترتیب انتخاب: Priority سمت، سپس EmployeeId.
/// این کلاس گردش‌کار را اجرا نمی‌کند و داده‌ی سازمانی را تغییر نمی‌دهد.
/// </summary>
public class EnterpriseActorResolver : IEnterpriseActorResolver
{
    private readonly PEMSDbContext _ctx;
    private readonly IRoleModuleActionService _actions;

    public EnterpriseActorResolver(PEMSDbContext ctx, IRoleModuleActionService actions)
    {
        _ctx = ctx;
        _actions = actions;
    }

    private sealed class Raw
    {
        public int EmployeeId { get; init; }
        public List<string> Reasons { get; } = new();
        public int Priority { get; set; } = int.MaxValue;
    }

    private sealed record EmpInfo(int EmployeeId, string Name, bool IsActive, int DepartmentId, int DisciplineId);

    // ------------------------------------------------------------------ Resolve

    public async Task<ActorResolutionResult> ResolveAsync(ActorResolutionRequest req, CancellationToken ct = default)
    {
        var now = req.AsOf ?? DateTime.Now;
        var result = new ActorResolutionResult { ResponsibilityKey = req.ResponsibilityKey };

        var cfg = await _ctx.WorkflowActorResponsibilities.AsNoTracking()
            .Include(x => x.Positions)
            .FirstOrDefaultAsync(x => x.ResponsibilityKey == req.ResponsibilityKey && x.IsActive, ct);
        if (cfg == null)
        {
            result.State = ActorResolutionStates.NoConfiguration;
            result.Detail = "No active responsibility configuration exists for this key.";
            return result;
        }
        result.Strategy = cfg.SelectionStrategy;

        // ---- ۱) کاندیدهای پایه
        var raw = new Dictionary<int, Raw>();
        var preExcluded = new List<(int Emp, string Code, string Detail)>();
        var informational = new List<(int Emp, string Code, string Detail)>();

        void Add(int emp, string reason, int priority = int.MaxValue)
        {
            if (!raw.TryGetValue(emp, out var r)) raw[emp] = r = new Raw { EmployeeId = emp };
            if (!r.Reasons.Contains(reason)) r.Reasons.Add(reason);
            if (priority < r.Priority) r.Priority = priority;
        }

        switch (cfg.Kind)
        {
            case ActorResponsibilityKinds.RequesterManager:
                return await ResolveManagerAsync(cfg, req, result, ct);

            case ActorResponsibilityKinds.AssignmentPool:
                foreach (var e in await _ctx.PartCatalogueApproverAssignments.AsNoTracking()
                             .Where(a => a.IsActive && a.StepKey == cfg.ResponsibilityKey).Select(a => a.EmployeeId).Distinct().ToListAsync(ct))
                    Add(e, "AssignmentPool");
                if (raw.Count == 0)
                {
                    result.State = ActorResolutionStates.NoConfiguration;
                    result.Detail = "The assignment pool is empty.";
                    return result;
                }
                break;

            case ActorResponsibilityKinds.PositionScope:
                if (cfg.Positions.Count == 0 && string.IsNullOrWhiteSpace(cfg.RoleName))
                {
                    result.State = ActorResolutionStates.NoConfiguration;
                    result.Detail = "Neither a position nor a role is configured.";
                    return result;
                }
                if (cfg.Positions.Count > 0)
                    await LoadPositionHoldersAsync(cfg.Positions.Select(p => (p.PositionId, p.Priority)), now, "", Add, preExcluded, ct);
                else
                    foreach (var e in await EmployeesInRoleAsync(cfg.RoleName!, ct)) Add(e, "Role:" + cfg.RoleName);
                break;

            case ActorResponsibilityKinds.CommodityPosition:
            {
                static int? Id(string? v) => int.TryParse(v, out var n) ? n : null;
                var group1 = Id(req.Context.GetValueOrDefault(ActorContextKeys.CommodityGroup1Id));
                var group2 = Id(req.Context.GetValueOrDefault(ActorContextKeys.CommodityGroup2Id));

                var allRows = await _ctx.CommodityGroupResponsibles.AsNoTracking()
                    .Include(x => x.Position)
                    .Where(x => x.ResponsibilityKey == cfg.ResponsibilityKey && x.IsActive).ToListAsync(ct);
                List<CommodityGroupResponsible> Valid(int g) => allRows.Where(x => x.CommodityGroupId == g
                    && (x.ValidFrom == null || x.ValidFrom <= now) && (x.ValidTo == null || x.ValidTo >= now)).ToList();

                // قاعده‌ی قطعی: ابتدا Group 1 (طبقه‌بندی اصلی)؛ فقط اگر برایش مسئولی تعریف نشده، Group 2
                var groups = new[] { group1, group2 }.Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
                var rows = group1.HasValue ? Valid(group1.Value) : new List<CommodityGroupResponsible>();
                if (rows.Count == 0 && group2.HasValue) rows = Valid(group2.Value);
                var coveredGroups = rows.Select(r => r.CommodityGroupId).ToHashSet();

                if (groups.Count == 0)
                {
                    result.State = ActorResolutionStates.NoEligibleActor;
                    result.Detail = "The business object has no commodity group; commodity responsibility cannot be resolved.";
                    return result;
                }
                if (rows.Count == 0)
                {
                    result.State = allRows.Count == 0 ? ActorResolutionStates.NoConfiguration : ActorResolutionStates.WrongCommodityScope;
                    result.Detail = allRows.Count == 0 ? "No commodity responsibility mapping exists." : "No active mapping covers this commodity group.";
                    return result;
                }

                foreach (var row in rows.OrderBy(r => r.Priority).ThenBy(r => r.CommodityGroupResponsibleId))
                {
                    if (row.EmployeeId.HasValue) Add(row.EmployeeId.Value, "CommodityOverride", row.Priority);
                    if (row.PositionId.HasValue)
                        await LoadPositionHoldersAsync(new[] { (row.PositionId.Value, row.Priority) }, now, "Commodity:", Add, preExcluded, ct);
                }

                // اطلاعات تشخیصی: مسئولان گروه‌های دیگر (برای مدیر روشن شود چرا واجد نیستند)
                var others = allRows.Where(x => !coveredGroups.Contains(x.CommodityGroupId)).ToList();
                foreach (var row in others)
                {
                    var emps = new List<int>();
                    if (row.EmployeeId.HasValue) emps.Add(row.EmployeeId.Value);
                    if (row.PositionId.HasValue)
                        emps.AddRange(await _ctx.EmployeeAssignments.AsNoTracking().Where(a => a.PositionId == row.PositionId && a.IsActive).Select(a => a.EmployeeId).ToListAsync(ct));
                    foreach (var e in emps.Distinct().Where(e => !raw.ContainsKey(e) && informational.All(i => i.Emp != e)))
                        informational.Add((e, ActorResolutionStates.WrongCommodityScope, $"Responsible for another commodity group ({row.CommodityGroupId})."));
                }
                break;
            }

            default:
                result.State = ActorResolutionStates.NoConfiguration;
                result.Detail = "Unknown responsibility kind.";
                return result;
        }

        // ---- ۲) فیلترها
        await ApplyFiltersAsync(cfg, req, raw, preExcluded, informational, result, ct);
        return result;
    }

    // ------------------------------------------------------------------ Filters + selection

    private async Task ApplyFiltersAsync(
        WorkflowActorResponsibility cfg, ActorResolutionRequest req, Dictionary<int, Raw> raw,
        List<(int Emp, string Code, string Detail)> preExcluded, List<(int Emp, string Code, string Detail)> informational,
        ActorResolutionResult result, CancellationToken ct)
    {
        var ids = raw.Keys.Concat(preExcluded.Select(x => x.Emp)).Concat(informational.Select(x => x.Emp)).Distinct().ToList();
        var emps = await LoadEmployeesAsync(ids, ct);
        string Name(int id) => emps.TryGetValue(id, out var e) ? e.Name : $"#{id}";

        var excluded = new List<(int Emp, string Code, string Detail)>(preExcluded);

        var scopeIds = ParseIds(cfg.ScopeValue);
        HashSet<int>? mgDisciplines = null;
        HashSet<int>? teamEmployees = null;
        if (cfg.ScopeType == ActorResponsibilityScopes.MaintenanceGroup)
            mgDisciplines = await DisciplinesOfGroupsAsync(scopeIds, ct);
        else if (cfg.ScopeType == ActorResponsibilityScopes.Team)
            teamEmployees = (await _ctx.TeamMembers.AsNoTracking()
                .Where(m => m.IsActive && m.LeftDate == null && scopeIds.Contains(m.TeamId)).Select(m => m.EmployeeId).ToListAsync(ct)).ToHashSet();

        if (cfg.ScopeType != ActorResponsibilityScopes.None && scopeIds.Count == 0)
        {
            result.State = ActorResolutionStates.NoConfiguration;
            result.Detail = "A scope type is configured without a scope value.";
            return;
        }

        var roleMap = await RolesOfEmployeesAsync(raw.Keys, ct);
        var survivors = new List<Raw>();

        foreach (var c in raw.Values.OrderBy(r => r.EmployeeId))
        {
            if (c.EmployeeId == req.RequesterEmployeeId)
            { excluded.Add((c.EmployeeId, ActorResolutionStates.RequesterOnly, "Separation of duties: the requester cannot act on their own request.")); continue; }

            if (!emps.TryGetValue(c.EmployeeId, out var emp) || !emp.IsActive)
            { excluded.Add((c.EmployeeId, ActorResolutionStates.InactiveActor, "Employee is inactive.")); continue; }

            var inScope = cfg.ScopeType switch
            {
                ActorResponsibilityScopes.Department => scopeIds.Contains(emp.DepartmentId),
                ActorResponsibilityScopes.Discipline => scopeIds.Contains(emp.DisciplineId),
                ActorResponsibilityScopes.MaintenanceGroup => mgDisciplines!.Contains(emp.DisciplineId),
                ActorResponsibilityScopes.Team => teamEmployees!.Contains(emp.EmployeeId),
                _ => true
            };
            if (!inScope)
            { excluded.Add((c.EmployeeId, ActorResolutionStates.WrongOrganizationScope, $"Outside the required {cfg.ScopeType} scope.")); continue; }

            roleMap.TryGetValue(c.EmployeeId, out var roles);
            roles ??= new List<string>();

            if (!string.IsNullOrWhiteSpace(cfg.RoleName) && !roles.Contains(cfg.RoleName, StringComparer.OrdinalIgnoreCase))
            { excluded.Add((c.EmployeeId, ActorResolutionStates.MissingPermission, $"Does not hold role '{cfg.RoleName}'.")); continue; }

            if (!string.IsNullOrWhiteSpace(cfg.RequiredModule) && !string.IsNullOrWhiteSpace(cfg.RequiredAction))
            {
                if (roles.Count == 0)
                { excluded.Add((c.EmployeeId, ActorResolutionStates.MissingPermission, "Employee has no user account/roles.")); continue; }
                if (!await _actions.HasActionAsync(roles, cfg.RequiredModule!, cfg.RequiredAction!))
                { excluded.Add((c.EmployeeId, ActorResolutionStates.MissingPermission, $"Lacks permission {cfg.RequiredModule}.{cfg.RequiredAction}.")); continue; }
            }

            survivors.Add(c);
        }

        foreach (var s in survivors)
            result.Candidates.Add(new ActorCandidate(s.EmployeeId, Name(s.EmployeeId), string.Join("; ", s.Reasons)));

        // ---- انتخاب قطعی
        IEnumerable<Raw> selected = survivors;
        if (cfg.SelectionStrategy == ActorSelectionStrategies.SingleActor && survivors.Count > 0)
            selected = survivors.OrderBy(s => s.Priority).ThenBy(s => s.EmployeeId).Take(1).ToList();
        result.SelectedEmployeeIds.AddRange(selected.Select(s => s.EmployeeId));

        foreach (var (emp, code, detail) in excluded.Concat(informational))
            result.Excluded.Add(new ActorExclusion(emp, Name(emp), code, detail));

        // ---- وضعیت تشخیصی
        if (survivors.Count == 1) result.State = ActorResolutionStates.Resolved;
        else if (survivors.Count > 1) result.State = ActorResolutionStates.MultipleEligibleActors;
        else
        {
            var codes = excluded.Select(e => e.Code).Distinct().ToList();
            if (excluded.Count == 0 && informational.Count > 0) result.State = ActorResolutionStates.WrongCommodityScope;
            else if (codes.Count == 1) result.State = codes[0];
            else result.State = ActorResolutionStates.NoEligibleActor;
            result.Detail ??= excluded.Count == 0 ? "Nobody currently holds this responsibility." : null;
        }
    }

    // ------------------------------------------------------------------ Requester manager chain (Organization hierarchy)

    /// <summary>
    /// مدیر مستقیم/مافوق فقط از سلسله‌مراتب سازمانی: درخواست‌دهنده ← سمت اصلیِ معتبر ← ParentPosition ← دارنده‌ی معتبر.
    /// بدون میان‌بر مدیرعامل، بدون Admin/Department/Team Supervisor/اولین مدیر موجود. هر شکستِ ساختاری = ORGANIZATION_RESOLUTION_REQUIRED
    /// با کد مشکل و مسیر قابل‌توضیح. سطح ۲ (Superior) = ParentPosition ِ ParentPosition.
    /// </summary>
    private async Task<ActorResolutionResult> ResolveManagerAsync(WorkflowActorResponsibility cfg, ActorResolutionRequest req, ActorResolutionResult result, CancellationToken ct)
    {
        var level = cfg.ManagerLevel is >= 2 ? cfg.ManagerLevel.Value : 1;
        var chain = await OrganizationManagerChain.WalkAsync(_ctx, req.RequesterEmployeeId, level, req.AsOf ?? DateTime.Now, ct);

        result.RequesterPositionId = chain.RequesterPositionId;
        result.ResolvedPositionId = chain.ResolvedPositionId;
        result.ResolutionPath.AddRange(chain.Path);

        if (chain.NoSuperior)
        {
            // مافوقِ مستقیم ریشه است ⇒ مافوق بالاتر وجود ندارد (فرض مستند) و مرحله‌ی «Superior» لازم نیست
            result.State = ActorResolutionStates.NotApplicable;
            result.SkipReason = "NoSuperiorManager";
            return result;
        }
        if (chain.IssueCode != null)
        {
            result.State = ActorResolutionStates.OrganizationResolutionRequired;
            result.OrganizationIssue = chain.IssueCode;
            result.Detail = chain.Detail;
            return result;
        }

        var rawMap = new Dictionary<int, Raw>();
        foreach (var h in chain.HolderEmployeeIds)
        {
            var r = new Raw { EmployeeId = h };
            r.Reasons.Add(level == 1 ? "DirectManager" : "SuperiorManager");
            rawMap[h] = r;
        }
        await ApplyFiltersAsync(cfg, req, rawMap, new(), new(), result, ct);
        return result;
    }

    // ------------------------------------------------------------------ Loaders

    private async Task LoadPositionHoldersAsync(
        IEnumerable<(int PositionId, int Priority)> positions, DateTime now, string reasonPrefix,
        Action<int, string, int> add, List<(int Emp, string Code, string Detail)> preExcluded, CancellationToken ct)
    {
        var map = positions.GroupBy(p => p.PositionId).ToDictionary(g => g.Key, g => g.Min(x => x.Priority));
        var ids = map.Keys.ToList();
        var titles = await _ctx.Positions.AsNoTracking().Where(p => ids.Contains(p.PositionId)).Select(p => new { p.PositionId, p.Title, p.IsActive }).ToListAsync(ct);
        var assignments = await _ctx.EmployeeAssignments.AsNoTracking().Where(a => ids.Contains(a.PositionId) && a.IsActive).ToListAsync(ct);

        foreach (var a in assignments.OrderBy(a => a.PositionId).ThenBy(a => a.EmployeeAssignmentId))
        {
            var pos = titles.FirstOrDefault(t => t.PositionId == a.PositionId);
            if (pos == null || !pos.IsActive)
            { preExcluded.Add((a.EmployeeId, ActorResolutionStates.InactiveActor, $"Position '{pos?.Title}' is inactive.")); continue; }
            if (a.StartDate > now || (a.EndDate.HasValue && a.EndDate.Value < now))
            { preExcluded.Add((a.EmployeeId, ActorResolutionStates.InactiveActor, $"Assignment to '{pos.Title}' is outside its validity period.")); continue; }
            add(a.EmployeeId, $"{reasonPrefix}Position:{pos.Title}", map[a.PositionId]);
        }
    }

    private async Task<List<int>> EmployeesInRoleAsync(string role, CancellationToken ct)
    {
        var q = from ur in _ctx.UserRoles
                join r in _ctx.Roles on ur.RoleId equals r.Id
                join u in _ctx.Users on ur.UserId equals u.Id
                where r.Name == role && u.EmployeeId != null
                select u.EmployeeId!.Value;
        return await q.Distinct().ToListAsync(ct);
    }

    private async Task<Dictionary<int, List<string>>> RolesOfEmployeesAsync(IEnumerable<int> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, List<string>>();
        var rows = await (from u in _ctx.Users
                          join ur in _ctx.UserRoles on u.Id equals ur.UserId
                          join r in _ctx.Roles on ur.RoleId equals r.Id
                          where u.EmployeeId != null && ids.Contains(u.EmployeeId.Value)
                          select new { Emp = u.EmployeeId!.Value, Role = r.Name! }).ToListAsync(ct);
        return rows.GroupBy(x => x.Emp).ToDictionary(g => g.Key, g => g.Select(x => x.Role).Distinct().ToList());
    }

    private async Task<Dictionary<int, EmpInfo>> LoadEmployeesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return (await _ctx.Employees.AsNoTracking().Where(e => list.Contains(e.EmployeeId))
                .Select(e => new { e.EmployeeId, e.FirstName, e.LastName, e.IsActive, e.DepartmentId, e.DisciplineId }).ToListAsync(ct))
            .ToDictionary(e => e.EmployeeId, e => new EmpInfo(e.EmployeeId, (e.FirstName + " " + e.LastName).Trim(), e.IsActive, e.DepartmentId, e.DisciplineId));
    }

    private async Task<HashSet<int>> DisciplinesOfGroupsAsync(HashSet<int> groupIds, CancellationToken ct)
    {
        var all = await _ctx.MaintenanceGroups.AsNoTracking().Select(g => new { g.MaintenanceGroupId, g.ParentMaintenanceGroupId, g.DisciplineId }).ToListAsync(ct);
        var result = new HashSet<int>();
        var frontier = new Queue<int>(groupIds);
        var seen = new HashSet<int>(groupIds);
        while (frontier.Count > 0)
        {
            var id = frontier.Dequeue();
            var g = all.FirstOrDefault(x => x.MaintenanceGroupId == id);
            if (g == null) continue;
            result.Add(g.DisciplineId);
            foreach (var child in all.Where(x => x.ParentMaintenanceGroupId == id && seen.Add(x.MaintenanceGroupId)))
                frontier.Enqueue(child.MaintenanceGroupId);
        }
        return result;
    }

    public static HashSet<int> ParseIds(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? (int?)n : null).Where(n => n.HasValue).Select(n => n!.Value).ToHashSet();

    // ------------------------------------------------------------------ Data quality

    public async Task<IReadOnlyList<ActorDataQualityFinding>> GetDataQualityAsync(CancellationToken ct = default)
    {
        var f = new List<ActorDataQualityFinding>();
        var now = DateTime.Now;
        var cfgs = await _ctx.WorkflowActorResponsibilities.AsNoTracking().Include(x => x.Positions).Where(x => x.IsActive).ToListAsync(ct);

        foreach (var c in cfgs)
        {
            void Add(string sev, string code, string msg) => f.Add(new ActorDataQualityFinding(sev, code, c.ResponsibilityKey, msg));

            if (c.Kind == ActorResponsibilityKinds.PositionScope)
            {
                if (c.Positions.Count == 0 && string.IsNullOrWhiteSpace(c.RoleName)) Add("P1", "RESPONSIBILITY_WITHOUT_POSITION", "No position or role is configured.");
                if (c.SelectionStrategy == ActorSelectionStrategies.SingleActor
                    && c.Positions.GroupBy(p => p.Priority).Any(g => g.Count() > 1)) Add("P3", "AMBIGUOUS_SINGLE_ACTOR", "Several positions share the same priority under SingleActor; ties are broken by employee id.");

                var posIds = c.Positions.Select(p => p.PositionId).ToList();
                var assigns = await _ctx.EmployeeAssignments.AsNoTracking().Where(a => posIds.Contains(a.PositionId) && a.IsActive).ToListAsync(ct);
                foreach (var p in c.Positions)
                {
                    var holder = assigns.FirstOrDefault(a => a.PositionId == p.PositionId);
                    var title = await _ctx.Positions.AsNoTracking().Where(x => x.PositionId == p.PositionId).Select(x => x.Title).FirstOrDefaultAsync(ct);
                    if (holder == null) { Add("P1", "POSITION_WITHOUT_EMPLOYEE", $"Position '{title}' has no active assignment."); continue; }
                    if (holder.EndDate.HasValue && holder.EndDate.Value < now) Add("P2", "EXPIRED_ASSIGNMENT", $"The assignment to '{title}' has expired.");
                    var emp = await _ctx.Employees.AsNoTracking().Where(e => e.EmployeeId == holder.EmployeeId).Select(e => new { e.IsActive }).FirstOrDefaultAsync(ct);
                    if (emp == null || !emp.IsActive) Add("P1", "INACTIVE_ASSIGNED_EMPLOYEE", $"The holder of '{title}' is inactive.");
                    else if (!string.IsNullOrWhiteSpace(c.RequiredModule) && !string.IsNullOrWhiteSpace(c.RequiredAction))
                    {
                        var roles = (await RolesOfEmployeesAsync(new[] { holder.EmployeeId }, ct)).GetValueOrDefault(holder.EmployeeId) ?? new List<string>();
                        if (roles.Count == 0 || !await _actions.HasActionAsync(roles, c.RequiredModule!, c.RequiredAction!))
                            Add("P2", "POSITION_WITHOUT_PERMISSION", $"The holder of '{title}' lacks {c.RequiredModule}.{c.RequiredAction}.");
                    }
                }
            }
            else if (c.Kind == ActorResponsibilityKinds.CommodityPosition)
            {
                var mapped = await _ctx.CommodityGroupResponsibles.AsNoTracking().Where(x => x.ResponsibilityKey == c.ResponsibilityKey && x.IsActive)
                    .Select(x => x.CommodityGroupId).Distinct().CountAsync(ct);
                var total = await _ctx.PartCatalogueCommodityGroups.AsNoTracking().CountAsync(g => g.IsActive, ct);
                if (mapped == 0) Add("P1", "COMMODITY_WITHOUT_RESPONSIBLE", "No commodity group has a responsible position/employee.");
                else if (mapped < total) Add("P2", "COMMODITY_PARTIALLY_MAPPED", $"{total - mapped} of {total} commodity groups have no responsible mapping.");
            }
            else if (c.Kind == ActorResponsibilityKinds.AssignmentPool)
            {
                var n = await _ctx.PartCatalogueApproverAssignments.AsNoTracking().Where(a => a.IsActive && a.StepKey == c.ResponsibilityKey).Select(a => a.EmployeeId).Distinct().CountAsync(ct);
                if (n == 0) Add("P1", "POOL_EMPTY", "The assignment pool is empty.");
                else if (n == 1) Add("P2", "POOL_SINGLE_PERSON", "Only one person is in the pool; separation of duties blocks them from their own requests.");
            }
        }
        return f;
    }
}
