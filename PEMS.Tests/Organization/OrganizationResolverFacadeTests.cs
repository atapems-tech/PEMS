using Microsoft.EntityFrameworkCore;
using PEMS.Application.Organization;
using PEMS.Domain.Common;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Persistence.Workflow;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Organization;

/// <summary>
/// Characterization of the CURRENT organizational resolution behavior (Work Order governance identity: Employee → Department →
/// OrganizationDomain, plus Discipline; manager resolution) and proof that the IOrganizationResolver facade is equivalent.
/// Expectations are literal values so they stay valid when callers are moved behind the facade.
/// </summary>
public class OrganizationResolverFacadeTests
{
    private static PEMSDbContext Db() => PemsDbContextFactory.CreateInMemory("res_" + Guid.NewGuid().ToString("N"));

    private sealed class World
    {
        public required PEMSDbContext Db { get; init; }
        public int MaintDept, ChildDept, OrphanDept, EngDept;
    }

    private static async Task<World> NewWorld()
    {
        var db = Db();
        var maint = new Department { Code = "M", Name = "Maintenance", OrganizationDomain = OrganizationDomains.Maintenance };
        var eng = new Department { Code = "E", Name = "Engineering", OrganizationDomain = OrganizationDomains.Engineering };
        var orphan = new Department { Code = "O", Name = "Orphan" };
        db.Departments.AddRange(maint, eng, orphan);
        await db.SaveChangesAsync();
        var child = new Department { Code = "MC", Name = "Maint child", ParentDepartmentId = maint.DepartmentId };
        db.Departments.Add(child);
        await db.SaveChangesAsync();
        return new World { Db = db, MaintDept = maint.DepartmentId, ChildDept = child.DepartmentId, OrphanDept = orphan.DepartmentId, EngDept = eng.DepartmentId };
    }

    private static Employee Emp(int id, int dept, int disc = 5, bool active = true) =>
        new() { EmployeeId = id, FirstName = "E" + id, LastName = "T", DepartmentId = dept, DisciplineId = disc, IsActive = active };

    // ------------------------------------------------------------------ characterization: Work Order governance identity

    [Fact]
    public async Task Characterization_EmployeeDomainAndDiscipline_CurrentBehavior()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept, 5), Emp(2, w.ChildDept, 6), Emp(3, w.OrphanDept, 7), Emp(4, w.EngDept, 8));
        await w.Db.SaveChangesAsync();
        var facade = new OrganizationResolver(w.Db);

        // literal expectations (current behavior)
        foreach (var (id, domain, disc) in new[] { (1, "Maintenance", 5), (2, "Maintenance", 6), (3, (string?)null, 7), (4, "Engineering", 8) })
        {
            Assert.Equal((domain, (int?)disc), await WorkOrderWorkflow.ResolveEmployeeAsync(w.Db, id));      // existing static path
            Assert.Equal((domain, (int?)disc), await facade.GetDomainAndDisciplineAsync(id));                  // facade equivalence
        }
        Assert.Equal(((string?)null, (int?)null), await WorkOrderWorkflow.ResolveEmployeeAsync(w.Db, 999));
        Assert.Equal(((string?)null, (int?)null), await facade.GetDomainAndDisciplineAsync(999));
        Assert.Equal(((string?)null, (int?)null), await WorkOrderWorkflow.ResolveEmployeeAsync(w.Db, null));
        Assert.Equal(((string?)null, (int?)null), await facade.GetDomainAndDisciplineAsync(null));
        Assert.Equal("Maintenance", await facade.ResolveDomainAsync(w.ChildDept));       // inherited from parent department
        Assert.Null(await facade.ResolveDomainAsync(w.OrphanDept));
    }

    [Fact]
    public async Task Characterization_ManagerResolution_FacadeEqualsChain()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept), Emp(3, w.MaintDept));
        await w.Db.SaveChangesAsync();
        await OrgSeed.ChainAsync(w.Db, 1, 2, 3);
        var facade = new OrganizationResolver(w.Db);

        var direct = await facade.ResolveManagerAsync(1);
        Assert.True(direct.Resolved); Assert.Equal(new[] { 2 }, direct.HolderEmployeeIds);
        Assert.Equal(new[] { 3 }, (await facade.ResolveManagerAsync(1, 2)).HolderEmployeeIds);
        Assert.True((await facade.ResolveManagerAsync(2, 2)).NoSuperior);                          // direct manager is root -> valid, no superior
        Assert.Equal(OrganizationIssueCodes.RootPosition, (await facade.ResolveManagerAsync(3)).IssueCode);
        var viaChain = await OrganizationManagerChain.WalkAsync(w.Db, 1, 1, DateTime.Now);
        Assert.Equal(viaChain.HolderEmployeeIds, direct.HolderEmployeeIds); Assert.Equal(viaChain.Path, direct.Path);
    }

    // ------------------------------------------------------------------ facade context

    [Fact]
    public async Task Context_UsesOnlyActivePrimaryAssignment_AndNeverGuessesWhenAmbiguous()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept, active: false));
        await w.Db.SaveChangesAsync();
        var ps = await OrgSeed.ChainAsync(w.Db, 1);
        var facade = new OrganizationResolver(w.Db);

        var ctx = await facade.GetContextAsync(1);
        Assert.True(ctx.Exists); Assert.Equal(ps[0], ctx.PrimaryPositionId); Assert.Equal("Maintenance", ctx.OrganizationDomain); Assert.Equal(5, ctx.DisciplineId);

        var extra = new Position { Title = "extra", IsActive = true }; w.Db.Positions.Add(extra); await w.Db.SaveChangesAsync();
        w.Db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = extra.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-1) });
        await w.Db.SaveChangesAsync();
        Assert.Null((await facade.GetContextAsync(1)).PrimaryPositionId);                          // two primaries: ambiguous -> none chosen
        Assert.False((await facade.GetContextAsync(404)).Exists);
        Assert.False((await facade.GetContextAsync(2)).IsActive);
    }

    [Fact]
    public async Task PositionHolders_ExcludeInactiveEmployeesAndOutOfRangeAssignments()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept, active: false), Emp(3, w.MaintDept));
        await w.Db.SaveChangesAsync();
        var p = new Position { Title = "p", IsActive = true }; w.Db.Positions.Add(p); await w.Db.SaveChangesAsync();
        // (IsPrimary=false on all: InMemory has no unique index; the SQL index is covered elsewhere)
        w.Db.EmployeeAssignments.AddRange(
            new EmployeeAssignment { PositionId = p.PositionId, EmployeeId = 1, IsActive = true, IsPrimary = false, StartDate = DateTime.Now.AddDays(-5) },
            new EmployeeAssignment { PositionId = p.PositionId, EmployeeId = 2, IsActive = true, IsPrimary = false, StartDate = DateTime.Now.AddDays(-5) },
            new EmployeeAssignment { PositionId = p.PositionId, EmployeeId = 3, IsActive = true, IsPrimary = false, StartDate = DateTime.Now.AddDays(5) });
        await w.Db.SaveChangesAsync();
        Assert.Equal(new[] { 1 }, await new OrganizationResolver(w.Db).GetPositionHoldersAsync(p.PositionId));
    }

    // ------------------------------------------------------------------ consistency diagnostics

    [Fact]
    public async Task DepartmentConsistency_ReportsAllFiveStatuses_ReadOnly()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept), Emp(3, w.MaintDept), Emp(4, w.MaintDept));
        await w.Db.SaveChangesAsync();
        Position P(string t, int? dept) { var p = new Position { Title = t, IsActive = true, DepartmentId = dept }; w.Db.Positions.Add(p); return p; }
        var same = P("same", w.MaintDept); var other = P("other", w.EngDept); var none = P("none", null);
        await w.Db.SaveChangesAsync();
        void A(int emp, Position p) => w.Db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = p.PositionId, EmployeeId = emp, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-1) });
        A(1, same); A(2, other); A(3, none);
        await w.Db.SaveChangesAsync();
        var before = await w.Db.Employees.AsNoTracking().Select(e => e.DepartmentId).ToListAsync();

        var rows = (await new OrganizationConsistencyService(w.Db).GetDepartmentConsistencyAsync()).ToDictionary(r => r.EmployeeId);
        Assert.Equal(DepartmentConsistencyStatuses.Consistent, rows[1].Status);
        Assert.Equal(DepartmentConsistencyStatuses.Conflict, rows[2].Status);
        Assert.Equal(DepartmentConsistencyStatuses.EmployeeOnly, rows[3].Status);
        Assert.False(rows.ContainsKey(4));                                                          // no primary position -> not evaluated here
        Assert.Equal(before, await w.Db.Employees.AsNoTracking().Select(e => e.DepartmentId).ToListAsync());   // nothing repaired
        // POSITION_ONLY / NO_DEPARTMENT: employee with an unknown (0) department
        w.Db.Employees.Add(Emp(5, 0)); w.Db.Employees.Add(Emp(6, 0)); await w.Db.SaveChangesAsync();
        A(5, P("posdept", w.EngDept)); A(6, P("nodept", null)); await w.Db.SaveChangesAsync();
        var rows2 = (await new OrganizationConsistencyService(w.Db).GetDepartmentConsistencyAsync()).ToDictionary(r => r.EmployeeId);
        Assert.Equal(DepartmentConsistencyStatuses.PositionOnly, rows2[5].Status);
        Assert.Equal(DepartmentConsistencyStatuses.NoDepartment, rows2[6].Status);
    }

    [Fact]
    public async Task LegacyPosition_ReportsMatchConflictInvalid_AndNothingWhenUnused()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept), Emp(3, w.MaintDept), Emp(4, w.MaintDept));
        await w.Db.SaveChangesAsync();
        Assert.Empty(await new OrganizationConsistencyService(w.Db).GetLegacyPositionConflictsAsync());   // NO_CONFLICT: legacy link unused

        var a = new Position { Title = "a", IsActive = true }; var b = new Position { Title = "b", IsActive = true }; var dead = new Position { Title = "dead", IsActive = false };
        w.Db.Positions.AddRange(a, b, dead); await w.Db.SaveChangesAsync();
        void Prim(int emp, Position p) => w.Db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = p.PositionId, EmployeeId = emp, IsActive = true, IsPrimary = true, StartDate = DateTime.Now.AddDays(-1) });
        Prim(1, a); Prim(2, b); Prim(3, a);
        (await w.Db.Employees.FindAsync(1))!.PositionId = a.PositionId;           // match
        (await w.Db.Employees.FindAsync(2))!.PositionId = a.PositionId;           // conflict (primary is b)
        (await w.Db.Employees.FindAsync(3))!.PositionId = dead.PositionId;        // invalid (inactive position)
        await w.Db.SaveChangesAsync();
        var rows = (await new OrganizationConsistencyService(w.Db).GetLegacyPositionConflictsAsync()).ToDictionary(r => r.EmployeeId);
        Assert.Equal(LegacyPositionStatuses.LegacyMatch, rows[1].Status);
        Assert.Equal(LegacyPositionStatuses.LegacyConflict, rows[2].Status);
        Assert.Equal(LegacyPositionStatuses.LegacyReferenceInvalid, rows[3].Status);
    }

    // ------------------------------------------------------------------ chart consumes the same authoritative structure

    [Fact]
    public async Task Chart_AgreesWithResolver_ForParentAndHolder()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept), Emp(3, w.MaintDept));
        await w.Db.SaveChangesAsync();
        await OrgSeed.ChainAsync(w.Db, 1, 2, 3);
        var resolver = new OrganizationResolver(w.Db);
        var chart = await new OrganizationHierarchyService(w.Db, new OrganizationValidationService(w.Db)).GetChartAsync();

        foreach (var emp in new[] { 1, 2 })
        {
            var ctx = await resolver.GetContextAsync(emp);
            var node = chart.Nodes.Single(n => n.PositionId == ctx.PrimaryPositionId);
            Assert.Equal(emp, node.EmployeeId);                                                       // same holder
            var parentNode = chart.Nodes.Single(n => n.Id == node.ParentId);
            Assert.Equal(ctx.ParentPositionId, parentNode.PositionId);                                // same parent
            var manager = await resolver.ResolveManagerAsync(emp);
            Assert.Equal(manager.HolderEmployeeIds, new[] { parentNode.EmployeeId!.Value });          // chart parent holder == Direct Manager
        }
    }

    // ------------------------------------------------------------------ position movement + lifecycle

    [Fact]
    public async Task PositionMove_ChangesParentWithoutDuplicating_IsValidated_AndAudited()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept), Emp(3, w.MaintDept));
        await w.Db.SaveChangesAsync();
        var ps = await OrgSeed.ChainAsync(w.Db, 1, 2, 3);                   // 1 -> 2 -> 3
        var svc = new OrganizationHierarchyService(w.Db, new OrganizationValidationService(w.Db));
        var before = await w.Db.Positions.CountAsync();

        Assert.True((await svc.SetPositionHierarchyAsync(ps[0], "T-1", ps[2])).Success);     // move position 1 directly under 3
        Assert.Equal(before, await w.Db.Positions.CountAsync());                              // moved, not duplicated
        Assert.Equal(ps[2], (await w.Db.Positions.AsNoTracking().SingleAsync(p => p.PositionId == ps[0])).ParentPositionId);
        Assert.False((await svc.SetPositionHierarchyAsync(ps[2], "T-3", ps[0])).Success);     // cycle rejected
        // the movement is in the audit trail
        Assert.Contains(await w.Db.AuditLogs.AsNoTracking().ToListAsync(),
            a => a.EntityName == "Position" && a.Action == "Update" && a.EntityKeyValue == ps[0].ToString() && (a.ChangedFieldsSummary ?? "").Contains("ParentPositionId"));
        // manager now resolves to the new parent's holder
        Assert.Equal(new[] { 3 }, (await new OrganizationResolver(w.Db).ResolveManagerAsync(1)).HolderEmployeeIds);
    }

    [Fact]
    public async Task PositionLifecycle_IsDerived_VacantOccupiedInactive_AndVacancyDoesNotDeletePosition()
    {
        var w = await NewWorld();
        w.Db.Employees.AddRange(Emp(1, w.MaintDept), Emp(2, w.MaintDept));
        await w.Db.SaveChangesAsync();
        var ps = await OrgSeed.ChainAsync(w.Db, 1, 2);
        var svc = new OrganizationHierarchyService(w.Db, new OrganizationValidationService(w.Db));
        Assert.Equal("OCCUPIED", (await svc.GetPositionRowsAsync()).Single(r => r.PositionId == ps[0]).Lifecycle);

        var a = await w.Db.EmployeeAssignments.SingleAsync(x => x.EmployeeId == 1);
        Assert.True((await svc.EndAssignmentAsync(a.EmployeeAssignmentId, "left")).Success);
        var rows = await svc.GetPositionRowsAsync();
        Assert.Equal("VACANT", rows.Single(r => r.PositionId == ps[0]).Lifecycle);              // position kept, history kept
        Assert.Equal(1, await w.Db.EmployeeAssignments.CountAsync(x => x.EmployeeId == 1));

        (await w.Db.Positions.FindAsync(ps[0]))!.IsActive = false; await w.Db.SaveChangesAsync();
        Assert.Equal("INACTIVE", (await svc.GetPositionRowsAsync()).Single(r => r.PositionId == ps[0]).Lifecycle);
    }
}
