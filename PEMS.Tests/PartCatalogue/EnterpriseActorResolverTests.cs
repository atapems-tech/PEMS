using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ActorResolution;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.PartCatalogue;

/// <summary>Enterprise Actor Resolver: Position/Role/Scope/Commodity/Manager، SoD، فعال‌بودن، اعتبار تخصیص، مجوز و انتخاب قطعی.</summary>
public class EnterpriseActorResolverTests
{
    private const string Key = "TestResp";
    private const int Requester = 1;

    private sealed class MapActions : IRoleModuleActionService
    {
        public HashSet<(string Role, string Module, string Action)> Grants { get; } = new();
        public Task<bool> HasActionAsync(IEnumerable<string> userRoles, string module, string action) =>
            Task.FromResult(userRoles.Any(r => Grants.Contains((r, module, action))));
        public Task SeedDefaultsAsync() => Task.CompletedTask;
    }

    private sealed class Fx
    {
        public required PEMSDbContext Db { get; init; }
        public required MapActions Actions { get; init; }
        public required EnterpriseActorResolver Resolver { get; init; }
        private int _roleSeq;
        private readonly Dictionary<string, int> _roleIds = new();

        public async Task<int> Emp(int id, int dept = 1, int disc = 1, bool active = true, params string[] roles)
        {
            Db.Employees.Add(new Employee { EmployeeId = id, FirstName = "E" + id, LastName = "T", DepartmentId = dept, DisciplineId = disc, IsActive = active });
            var u = new ApplicationUser { UserName = "u" + id, NormalizedUserName = "U" + id, EmployeeId = id, FullName = "u" + id };
            Db.Users.Add(u);
            await Db.SaveChangesAsync();
            foreach (var role in roles)
            {
                if (!_roleIds.TryGetValue(role, out var rid))
                {
                    var r = new ApplicationRole(role) { NormalizedName = role.ToUpperInvariant() };
                    Db.Roles.Add(r);
                    await Db.SaveChangesAsync();
                    _roleIds[role] = rid = r.Id;
                    _roleSeq++;
                }
                Db.UserRoles.Add(new IdentityUserRole<int> { UserId = u.Id, RoleId = rid });
            }
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task<int> Position(string title, int? holder, DateTime? start = null, DateTime? end = null, bool active = true)
        {
            var p = new Position { Title = title, IsActive = true };
            Db.Positions.Add(p);
            await Db.SaveChangesAsync();
            if (holder.HasValue)
                Db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = p.PositionId, EmployeeId = holder.Value, IsActive = active, StartDate = start ?? DateTime.Now.AddYears(-1), EndDate = end });
            await Db.SaveChangesAsync();
            return p.PositionId;
        }

        public async Task<WorkflowActorResponsibility> Resp(string kind = ActorResponsibilityKinds.PositionScope, string strategy = ActorSelectionStrategies.Queue,
            string scope = ActorResponsibilityScopes.None, string? scopeValue = null, string? role = null, string? perm = null, int[]? positions = null, int? level = null)
        {
            var r = new WorkflowActorResponsibility
            {
                ResponsibilityKey = Key, DisplayName = Key, Kind = kind, SelectionStrategy = strategy, ScopeType = scope, ScopeValue = scopeValue,
                RoleName = role, ManagerLevel = level, RequiredModule = perm == null ? null : "PartCatalogue", RequiredAction = perm
            };
            for (var i = 0; i < (positions?.Length ?? 0); i++)
                r.Positions.Add(new WorkflowActorResponsibilityPosition { PositionId = positions![i], Priority = i + 1 });
            Db.WorkflowActorResponsibilities.Add(r);
            await Db.SaveChangesAsync();
            return r;
        }

        public Task<ActorResolutionResult> Resolve(int requester = Requester, Dictionary<string, string?>? ctx = null) =>
            Resolver.ResolveAsync(new ActorResolutionRequest(Key, requester, ctx ?? new Dictionary<string, string?>()));
    }

    private static async Task<Fx> NewFx()
    {
        var db = PemsDbContextFactory.CreateInMemory("ar_" + Guid.NewGuid().ToString("N"));
        var actions = new MapActions();
        var fx = new Fx { Db = db, Actions = actions, Resolver = new EnterpriseActorResolver(db, actions) };
        await fx.Emp(Requester, roles: "Reviewer");
        return fx;
    }

    [Fact]
    public async Task NoConfiguration_WhenResponsibilityDoesNotExist()
    {
        var fx = await NewFx();
        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.NoConfiguration, r.State);
        Assert.False(r.IsUsable);
    }

    [Fact]
    public async Task PositionHolder_Resolves_WithReason()
    {
        var fx = await NewFx();
        var emp = await fx.Emp(2, roles: "Reviewer");
        var pos = await fx.Position("Spare Parts Specialist", emp);
        await fx.Resp(positions: new[] { pos });

        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.Resolved, r.State);
        var c = Assert.Single(r.Candidates);
        Assert.Equal(2, c.EmployeeId);
        Assert.Contains("Spare Parts Specialist", c.Reason);
    }

    [Fact]
    public async Task EmptyPosition_IsNoEligibleActor_NotNoConfiguration()
    {
        var fx = await NewFx();
        var pos = await fx.Position("Vacant", null);
        await fx.Resp(positions: new[] { pos });
        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.NoEligibleActor, r.State);
    }

    [Fact]
    public async Task PositionWithoutPositionOrRole_IsNoConfiguration()
    {
        var fx = await NewFx();
        await fx.Resp();
        Assert.Equal(ActorResolutionStates.NoConfiguration, (await fx.Resolve()).State);
    }

    [Fact]
    public async Task RoleOnly_ResolvesUsersInRole()
    {
        var fx = await NewFx();
        await fx.Emp(2, roles: "Cataloguer");
        await fx.Emp(3, roles: "Other");
        await fx.Resp(role: "Cataloguer");
        var r = await fx.Resolve();
        Assert.Equal(new[] { 2 }, r.Candidates.Select(c => c.EmployeeId).ToArray());
    }

    [Fact]
    public async Task PositionAndRole_BothRequired()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer");
        var pos = await fx.Position("P", e2);
        await fx.Resp(positions: new[] { pos }, role: "Cataloguer");
        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.MissingPermission, r.State);
    }

    [Fact]
    public async Task RequesterIsNeverEligible_AndRequesterOnlyIsDiagnosed()
    {
        var fx = await NewFx();
        var pos = await fx.Position("P", Requester);
        await fx.Resp(positions: new[] { pos });
        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.RequesterOnly, r.State);
        Assert.Contains(r.Excluded, x => x.EmployeeId == Requester && x.Code == ActorResolutionStates.RequesterOnly);
    }

    [Fact]
    public async Task InactiveEmployee_IsExcluded()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, active: false, roles: "Reviewer");
        var pos = await fx.Position("P", e2);
        await fx.Resp(positions: new[] { pos });
        Assert.Equal(ActorResolutionStates.InactiveActor, (await fx.Resolve()).State);
    }

    [Fact]
    public async Task ExpiredAndFutureAssignments_AreExcluded()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer");
        var e3 = await fx.Emp(3, roles: "Reviewer");
        var expired = await fx.Position("Expired", e2, end: DateTime.Now.AddDays(-1));
        var future = await fx.Position("Future", e3, start: DateTime.Now.AddDays(5));
        await fx.Resp(positions: new[] { expired, future });
        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.InactiveActor, r.State);
        Assert.Equal(2, r.Excluded.Count);
    }

    [Fact]
    public async Task InactiveAssignmentRow_IsIgnored()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer");
        var pos = await fx.Position("P", e2, active: false);
        await fx.Resp(positions: new[] { pos });
        Assert.Equal(ActorResolutionStates.NoEligibleActor, (await fx.Resolve()).State);
    }

    [Fact]
    public async Task MissingPermission_IsExcluded_AndGrantedPermissionPasses()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Plain");
        var e3 = await fx.Emp(3, roles: "Reviewer");
        var p2 = await fx.Position("P2", e2);
        var p3 = await fx.Position("P3", e3);
        fx.Actions.Grants.Add(("Reviewer", "PartCatalogue", "Review"));
        await fx.Resp(positions: new[] { p2, p3 }, perm: "Review");

        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.Resolved, r.State);
        Assert.Equal(3, Assert.Single(r.Candidates).EmployeeId);
        Assert.Contains(r.Excluded, x => x.EmployeeId == 2 && x.Code == ActorResolutionStates.MissingPermission);
    }

    [Fact]
    public async Task EmployeeWithoutUserAccount_FailsPermissionCheck()
    {
        var fx = await NewFx();
        fx.Db.Employees.Add(new Employee { EmployeeId = 5, FirstName = "NoUser", LastName = "T", IsActive = true });
        await fx.Db.SaveChangesAsync();
        var pos = await fx.Position("P", 5);
        await fx.Resp(positions: new[] { pos }, perm: "Review");
        Assert.Equal(ActorResolutionStates.MissingPermission, (await fx.Resolve()).State);
    }

    [Fact]
    public async Task DepartmentScope_FiltersByEmployeeDepartment()
    {
        var fx = await NewFx();
        var inDept = await fx.Emp(2, dept: 7, roles: "Reviewer");
        var outDept = await fx.Emp(3, dept: 8, roles: "Reviewer");
        var p1 = await fx.Position("A", inDept); var p2 = await fx.Position("B", outDept);
        await fx.Resp(positions: new[] { p1, p2 }, scope: ActorResponsibilityScopes.Department, scopeValue: "7");

        var r = await fx.Resolve();
        Assert.Equal(2, Assert.Single(r.Candidates).EmployeeId);
        Assert.Contains(r.Excluded, x => x.EmployeeId == 3 && x.Code == ActorResolutionStates.WrongOrganizationScope);
    }

    [Fact]
    public async Task WrongOrganizationScope_IsDiagnosed_WhenNobodyMatches()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, dept: 9, roles: "Reviewer");
        var pos = await fx.Position("P", e2);
        await fx.Resp(positions: new[] { pos }, scope: ActorResponsibilityScopes.Department, scopeValue: "7");
        Assert.Equal(ActorResolutionStates.WrongOrganizationScope, (await fx.Resolve()).State);
    }

    [Fact]
    public async Task ScopeTypeWithoutValue_IsNoConfiguration()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer");
        var pos = await fx.Position("P", e2);
        await fx.Resp(positions: new[] { pos }, scope: ActorResponsibilityScopes.Department);
        Assert.Equal(ActorResolutionStates.NoConfiguration, (await fx.Resolve()).State);
    }

    [Fact]
    public async Task DisciplineScope_AndMaintenanceGroupScope_UseDiscipline()
    {
        var fx = await NewFx();
        var mech = await fx.Emp(2, disc: 11, roles: "Reviewer");
        var elec = await fx.Emp(3, disc: 12, roles: "Reviewer");
        var p1 = await fx.Position("Mech", mech); var p2 = await fx.Position("Elec", elec);
        fx.Db.MaintenanceGroups.Add(new MaintenanceGroup { Code = "MG1", Name = "Mechanical group", DisciplineId = 11 });
        await fx.Db.SaveChangesAsync();
        var mgId = fx.Db.MaintenanceGroups.Single().MaintenanceGroupId;

        var resp = await fx.Resp(positions: new[] { p1, p2 }, scope: ActorResponsibilityScopes.Discipline, scopeValue: "12");
        Assert.Equal(3, Assert.Single((await fx.Resolve()).Candidates).EmployeeId);

        resp.ScopeType = ActorResponsibilityScopes.MaintenanceGroup; resp.ScopeValue = mgId.ToString();
        await fx.Db.SaveChangesAsync();
        Assert.Equal(2, Assert.Single((await fx.Resolve()).Candidates).EmployeeId);
    }

    [Fact]
    public async Task TeamScope_UsesActiveTeamMembership()
    {
        var fx = await NewFx();
        var member = await fx.Emp(2, roles: "Reviewer");
        var outsider = await fx.Emp(3, roles: "Reviewer");
        var left = await fx.Emp(4, roles: "Reviewer");
        var p = new[] { await fx.Position("A", member), await fx.Position("B", outsider), await fx.Position("C", left) };
        fx.Db.TeamMembers.AddRange(
            new TeamMember { TeamId = 5, EmployeeId = 2, RoleId = 1, IsActive = true },
            new TeamMember { TeamId = 5, EmployeeId = 4, RoleId = 1, IsActive = true, LeftDate = DateTime.Now.AddDays(-3) });
        await fx.Db.SaveChangesAsync();
        await fx.Resp(positions: p, scope: ActorResponsibilityScopes.Team, scopeValue: "5");
        Assert.Equal(new[] { 2 }, (await fx.Resolve()).Candidates.Select(c => c.EmployeeId).ToArray());
    }

    [Fact]
    public async Task MultipleEligible_Queue_AllSelected_StateMultiple()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer"); var e3 = await fx.Emp(3, roles: "Reviewer");
        await fx.Resp(positions: new[] { await fx.Position("A", e2), await fx.Position("B", e3) });
        var r = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.MultipleEligibleActors, r.State);
        Assert.True(r.IsUsable);
        Assert.Equal(new[] { 2, 3 }, r.SelectedEmployeeIds.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task SingleActor_IsDeterministic_ByPositionPriorityThenEmployeeId()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer"); var e3 = await fx.Emp(3, roles: "Reviewer");
        var first = await fx.Position("First priority", e3);
        var second = await fx.Position("Second priority", e2);
        await fx.Resp(strategy: ActorSelectionStrategies.SingleActor, positions: new[] { first, second });

        for (var i = 0; i < 5; i++)
        {
            var r = await fx.Resolve();
            Assert.Equal(3, Assert.Single(r.SelectedEmployeeIds));       // اولویت سمت، نه ترتیب EmployeeId
            Assert.Equal(2, r.Candidates.Count);                           // هر دو واجد شرایط، یکی انتخاب شده
        }
    }

    [Fact]
    public async Task SingleActor_TieBreak_IsLowestEmployeeId()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer"); var e3 = await fx.Emp(3, roles: "Reviewer");
        // یک سمت دو بار با همان اولویت امکان ندارد؛ دو سمتِ هم‌اولویت از طریق دو ردیف مستقیم
        var pA = await fx.Position("A", e3); var pB = await fx.Position("B", e2);
        var r = await fx.Resp(strategy: ActorSelectionStrategies.SingleActor, positions: new[] { pA, pB });
        foreach (var p in fx.Db.WorkflowActorResponsibilityPositions) p.Priority = 1;
        await fx.Db.SaveChangesAsync();
        Assert.Equal(2, Assert.Single((await fx.Resolve()).SelectedEmployeeIds));
    }

    // ------------------------------------------------------------------ Commodity

    private static Dictionary<string, string?> Group(int id) => new() { [ActorContextKeys.CommodityGroup1Id] = id.ToString() };

    [Fact]
    public async Task CommodityPosition_ResolvesByGroupMapping_AndRejectsOtherGroups()
    {
        var fx = await NewFx();
        var elec = await fx.Emp(2, roles: "Reviewer"); var mech = await fx.Emp(3, roles: "Reviewer");
        var pElec = await fx.Position("Electrical reviewer", elec); var pMech = await fx.Position("Mechanical reviewer", mech);
        fx.Db.PartCatalogueCommodityGroups.AddRange(new PartCatalogueCommodityGroup { Code = "1", Description = "Electrical" }, new PartCatalogueCommodityGroup { Code = "2", Description = "Mechanical" });
        await fx.Db.SaveChangesAsync();
        var gE = fx.Db.PartCatalogueCommodityGroups.Single(g => g.Code == "1").CommodityGroupId;
        var gM = fx.Db.PartCatalogueCommodityGroups.Single(g => g.Code == "2").CommodityGroupId;
        fx.Db.CommodityGroupResponsibles.AddRange(
            new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = gE, PositionId = pElec },
            new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = gM, PositionId = pMech });
        await fx.Db.SaveChangesAsync();
        await fx.Resp(ActorResponsibilityKinds.CommodityPosition);

        var e = await fx.Resolve(ctx: Group(gE));
        Assert.Equal(2, Assert.Single(e.Candidates).EmployeeId);
        Assert.Contains(e.Excluded, x => x.EmployeeId == 3 && x.Code == ActorResolutionStates.WrongCommodityScope);   // تشخیصی

        var m = await fx.Resolve(ctx: Group(gM));
        Assert.Equal(3, Assert.Single(m.Candidates).EmployeeId);
    }

    [Fact]
    public async Task CommodityPosition_UnmappedGroup_IsWrongCommodityScope_AndNoMappingsIsNoConfiguration()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer");
        var pos = await fx.Position("Elec", e2);
        fx.Db.PartCatalogueCommodityGroups.AddRange(new PartCatalogueCommodityGroup { Code = "1", Description = "A" }, new PartCatalogueCommodityGroup { Code = "2", Description = "B" });
        await fx.Db.SaveChangesAsync();
        var g1 = fx.Db.PartCatalogueCommodityGroups.Single(g => g.Code == "1").CommodityGroupId;
        var g2 = fx.Db.PartCatalogueCommodityGroups.Single(g => g.Code == "2").CommodityGroupId;
        await fx.Resp(ActorResponsibilityKinds.CommodityPosition);

        Assert.Equal(ActorResolutionStates.NoConfiguration, (await fx.Resolve(ctx: Group(g1))).State);

        fx.Db.CommodityGroupResponsibles.Add(new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = g1, PositionId = pos });
        await fx.Db.SaveChangesAsync();
        Assert.Equal(ActorResolutionStates.WrongCommodityScope, (await fx.Resolve(ctx: Group(g2))).State);
        Assert.Equal(ActorResolutionStates.NoEligibleActor, (await fx.Resolve(ctx: new Dictionary<string, string?>())).State);   // بدون Commodity در Context
    }

    [Fact]
    public async Task CommodityEmployeeOverride_IsAuditableAndStillSubjectToSoDAndPermission()
    {
        var fx = await NewFx();
        var reviewer = await fx.Emp(2, roles: "Reviewer"); var plain = await fx.Emp(3, roles: "Plain");
        fx.Db.PartCatalogueCommodityGroups.Add(new PartCatalogueCommodityGroup { Code = "1", Description = "A" });
        await fx.Db.SaveChangesAsync();
        var g = fx.Db.PartCatalogueCommodityGroups.Single().CommodityGroupId;
        fx.Actions.Grants.Add(("Reviewer", "PartCatalogue", "Review"));
        fx.Db.CommodityGroupResponsibles.AddRange(
            new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = g, EmployeeId = reviewer, Priority = 1 },
            new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = g, EmployeeId = plain, Priority = 2 },
            new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = g, EmployeeId = Requester, Priority = 3 },
            new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = g, EmployeeId = 99, Priority = 4, IsActive = false });
        await fx.Db.SaveChangesAsync();
        await fx.Resp(ActorResponsibilityKinds.CommodityPosition, perm: "Review");

        var r = await fx.Resolve(ctx: Group(g));
        Assert.Equal(2, Assert.Single(r.Candidates).EmployeeId);
        Assert.Contains(r.Excluded, x => x.EmployeeId == 3 && x.Code == ActorResolutionStates.MissingPermission);
        Assert.Contains(r.Excluded, x => x.EmployeeId == Requester && x.Code == ActorResolutionStates.RequesterOnly);
        Assert.DoesNotContain(r.Excluded, x => x.EmployeeId == 99);   // ردیف غیرفعال دیده نمی‌شود
    }

    [Fact]
    public async Task CommodityMapping_ValidityWindow_IsRespected()
    {
        var fx = await NewFx();
        var e2 = await fx.Emp(2, roles: "Reviewer");
        fx.Db.PartCatalogueCommodityGroups.Add(new PartCatalogueCommodityGroup { Code = "1", Description = "A" });
        await fx.Db.SaveChangesAsync();
        var g = fx.Db.PartCatalogueCommodityGroups.Single().CommodityGroupId;
        fx.Db.CommodityGroupResponsibles.Add(new CommodityGroupResponsible { ResponsibilityKey = Key, CommodityGroupId = g, EmployeeId = e2, ValidTo = DateTime.Now.AddDays(-1) });
        await fx.Db.SaveChangesAsync();
        await fx.Resp(ActorResponsibilityKinds.CommodityPosition);
        Assert.False((await fx.Resolve(ctx: Group(g))).IsUsable);
    }

    // ------------------------------------------------------------------ Manager chain / pool

    [Fact]
    public async Task ManagerChain_ResolvesDirectAndSuperior_FromPositionHierarchy_NoCeoShortcut()
    {
        var fx = await NewFx();
        await fx.Emp(2, roles: "Reviewer"); await fx.Emp(3, roles: "Reviewer"); await fx.Emp(4, roles: "Reviewer");
        await OrgSeed.ChainAsync(fx.Db, Requester, 2, 3, 4);

        var resp = await fx.Resp(ActorResponsibilityKinds.RequesterManager, level: 1);
        var r1 = await fx.Resolve();
        Assert.Equal(2, Assert.Single(r1.Candidates).EmployeeId);
        Assert.Contains(r1.ResolutionPath, p => p.StartsWith("Primary Position"));
        Assert.Contains(r1.ResolutionPath, p => p.StartsWith("Parent Position"));
        Assert.NotNull(r1.RequesterPositionId); Assert.NotNull(r1.ResolvedPositionId);

        resp.ManagerLevel = 2; await fx.Db.SaveChangesAsync();
        Assert.Equal(3, Assert.Single((await fx.Resolve()).Candidates).EmployeeId);   // Superior = grandparent

        // «CEO» دیگر مفهومی در Resolver نیست: ثبت CEO در جدول قدیمی هیچ اثری ندارد
        fx.Db.PartCatalogueApproverAssignments.Add(new PartCatalogueApproverAssignment { StepKey = "CEO", EmployeeId = 3 });
        await fx.Db.SaveChangesAsync();
        var still = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.Resolved, still.State);
        Assert.Equal(3, Assert.Single(still.Candidates).EmployeeId);
    }

    [Fact]
    public async Task ManagerChain_StructuralProblems_AreOrganizationResolutionRequired_ButMissingSuperiorIsNotApplicable()
    {
        var fx = await NewFx();
        var resp = await fx.Resp(ActorResponsibilityKinds.RequesterManager, level: 1);

        // کارمند بدون سمت اصلی (و بدون هیچ جدول محلی) ⇒ نیازمند تکمیل ساختار؛ نه مدیر حدسی
        var r1 = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.OrganizationResolutionRequired, r1.State);
        Assert.Equal("EMPLOYEE_NO_PRIMARY_POSITION", r1.OrganizationIssue);
        Assert.Empty(r1.Candidates);

        // مدیر مستقیم ریشه است ⇒ سطح ۲: Superior ندارد ⇒ NotApplicable (فرض مستند)
        await fx.Emp(2, roles: "Reviewer");
        await OrgSeed.ChainAsync(fx.Db, Requester, 2);
        resp.ManagerLevel = 2; await fx.Db.SaveChangesAsync();
        var r2 = await fx.Resolve();
        Assert.Equal(ActorResolutionStates.NotApplicable, r2.State);
        Assert.Equal("NoSuperiorManager", r2.SkipReason);

        // خود درخواست‌دهنده ریشه است ⇒ سطح ۱: مدیر مستقیم ندارد ⇒ نیازمند تکمیل ساختار (نه Skip، نه تأیید خودکار)
        resp.ManagerLevel = 1; await fx.Db.SaveChangesAsync();
        var r3 = await fx.Resolve(requester: 2);
        Assert.Equal(ActorResolutionStates.OrganizationResolutionRequired, r3.State);
        Assert.Equal("ROOT_POSITION_NO_PARENT", r3.OrganizationIssue);
    }

    [Fact]
    public async Task AssignmentPool_Legacy_MinusRequester_AndEmptyPoolIsNoConfiguration()
    {
        var fx = await NewFx();
        await fx.Resp(ActorResponsibilityKinds.AssignmentPool);
        Assert.Equal(ActorResolutionStates.NoConfiguration, (await fx.Resolve()).State);

        await fx.Emp(2, roles: "Reviewer");
        fx.Db.PartCatalogueApproverAssignments.AddRange(
            new PartCatalogueApproverAssignment { StepKey = Key, EmployeeId = Requester },
            new PartCatalogueApproverAssignment { StepKey = Key, EmployeeId = 2 });
        await fx.Db.SaveChangesAsync();
        Assert.Equal(2, Assert.Single((await fx.Resolve()).Candidates).EmployeeId);

        fx.Db.PartCatalogueApproverAssignments.Remove(fx.Db.PartCatalogueApproverAssignments.Single(a => a.EmployeeId == 2));
        await fx.Db.SaveChangesAsync();
        Assert.Equal(ActorResolutionStates.RequesterOnly, (await fx.Resolve()).State);   // دقیقاً حالت چهار خطای گزارش‌شده
    }

    // ------------------------------------------------------------------ Data quality

    [Fact]
    public async Task DataQuality_FlagsEmptyPositionsInactiveHoldersAndSinglePersonPools()
    {
        var fx = await NewFx();
        var vacant = await fx.Position("Vacant", null);
        var inactiveHolder = await fx.Position("Held by inactive", await fx.Emp(2, active: false, roles: "Reviewer"));
        fx.Db.WorkflowActorResponsibilities.Add(new WorkflowActorResponsibility { ResponsibilityKey = "A", DisplayName = "A", Kind = ActorResponsibilityKinds.PositionScope });
        var withPos = new WorkflowActorResponsibility { ResponsibilityKey = "B", DisplayName = "B", Kind = ActorResponsibilityKinds.PositionScope };
        withPos.Positions.Add(new WorkflowActorResponsibilityPosition { PositionId = vacant, Priority = 1 });
        withPos.Positions.Add(new WorkflowActorResponsibilityPosition { PositionId = inactiveHolder, Priority = 2 });
        fx.Db.WorkflowActorResponsibilities.Add(withPos);
        fx.Db.WorkflowActorResponsibilities.Add(new WorkflowActorResponsibility { ResponsibilityKey = "C", DisplayName = "C", Kind = ActorResponsibilityKinds.AssignmentPool });
        fx.Db.WorkflowActorResponsibilities.Add(new WorkflowActorResponsibility { ResponsibilityKey = "D", DisplayName = "D", Kind = ActorResponsibilityKinds.CommodityPosition });
        fx.Db.PartCatalogueApproverAssignments.Add(new PartCatalogueApproverAssignment { StepKey = "C", EmployeeId = 2 });
        await fx.Db.SaveChangesAsync();

        var codes = (await fx.Resolver.GetDataQualityAsync()).Select(f => (f.ResponsibilityKey, f.Code)).ToList();
        Assert.Contains(("A", "RESPONSIBILITY_WITHOUT_POSITION"), codes);
        Assert.Contains(("B", "POSITION_WITHOUT_EMPLOYEE"), codes);
        Assert.Contains(("B", "INACTIVE_ASSIGNED_EMPLOYEE"), codes);
        Assert.Contains(("C", "POOL_SINGLE_PERSON"), codes);
        Assert.Contains(("D", "COMMODITY_WITHOUT_RESPONSIBLE"), codes);
    }

    [Fact]
    public async Task EmployeeTransfer_ChangesFutureResolutionWithoutTouchingConfiguration()
    {
        var fx = await NewFx();
        var a = await fx.Emp(2, roles: "Reviewer"); var b = await fx.Emp(3, roles: "Reviewer");
        var pos = await fx.Position("Seat", a);
        await fx.Resp(positions: new[] { pos });
        Assert.Equal(2, Assert.Single((await fx.Resolve()).Candidates).EmployeeId);

        var assignment = await fx.Db.EmployeeAssignments.SingleAsync();
        assignment.IsActive = false; assignment.EndDate = DateTime.Now;
        fx.Db.EmployeeAssignments.Add(new EmployeeAssignment { PositionId = pos, EmployeeId = b, IsActive = true, StartDate = DateTime.Now.AddMinutes(-1) });
        await fx.Db.SaveChangesAsync();

        Assert.Equal(3, Assert.Single((await fx.Resolve()).Candidates).EmployeeId);
    }
}
