using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Organization;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using DisciplineController = PEMS.Web.Controllers.DisciplineController;
using EmployeeController = PEMS.Web.Controllers.EmployeeController;

namespace PEMS.Tests.Organization;

/// <summary>Position master data (Admin/Organization/Positions), employee ↔ position assignment from the Employee UI, discoverability (sidebar), authorization.</summary>
public class PositionManagementUiTests
{
    private static PEMSDbContext Db() => PemsDbContextFactory.CreateInMemory("pmu_" + Guid.NewGuid().ToString("N"));
    private static OrganizationHierarchyService Svc(PEMSDbContext db) => new(db, new OrganizationValidationService(db));

    private static async Task<(int Dept, int Disc)> Org(PEMSDbContext db)
    {
        var d = new Department { Code = "D", Name = "Dept" }; var x = new Discipline { Code = "ME", Name = "Mechanical", IsActive = true };
        db.Departments.Add(d); db.Disciplines.Add(x); await db.SaveChangesAsync(); return (d.DepartmentId, x.DisciplineId);
    }

    // ------------------------------------------------------------------ Position service

    [Fact]
    public async Task Position_Create_Edit_Activate_Deactivate_AndValidation()
    {
        var db = Db(); var (dept, _) = await Org(db); var svc = Svc(db);

        Assert.True((await svc.CreatePositionAsync("Maintenance Manager", "MNT-MGR", dept, "heads maintenance", null)).Success);
        Assert.True((await svc.CreatePositionAsync("Planner", "MNT-PLN", null, null, null)).Success);             // department optional
        Assert.Equal("Org_Err_DuplicatePositionCode", (await svc.CreatePositionAsync("Other", "MNT-MGR", null, null, null)).ErrorKey);
        Assert.Equal("Org_Err_TitleRequired", (await svc.CreatePositionAsync("  ", "X1", null, null, null)).ErrorKey);
        Assert.Equal("Org_Err_DepartmentInvalid", (await svc.CreatePositionAsync("T", "X2", 999, null, null)).ErrorKey);
        Assert.False((await svc.CreatePositionAsync("T", "X3", null, null, 999)).Success);                          // unknown parent

        var mgr = await db.Positions.SingleAsync(p => p.PositionCode == "MNT-MGR");
        Assert.True((await svc.UpdatePositionAsync(mgr.PositionId, "Head of Maintenance", "MNT-MGR", dept, "d2")).Success);
        Assert.Equal("Head of Maintenance", (await db.Positions.AsNoTracking().SingleAsync(p => p.PositionId == mgr.PositionId)).Title);
        Assert.Equal("Org_Err_DuplicatePositionCode", (await svc.UpdatePositionAsync(mgr.PositionId, "Head", "MNT-PLN", dept, null)).ErrorKey);
        Assert.Equal("Org_Err_PositionNotFound", (await svc.UpdatePositionAsync(9999, "x", null, null, null)).ErrorKey);

        Assert.True((await svc.SetPositionActiveAsync(mgr.PositionId, false)).Success);
        Assert.False((await db.Positions.AsNoTracking().SingleAsync(p => p.PositionId == mgr.PositionId)).IsActive);
        Assert.True((await svc.SetPositionActiveAsync(mgr.PositionId, true)).Success);
        Assert.Equal(3 - 1, await db.Positions.CountAsync());                                                      // never deleted (2 valid creates)
    }

    [Fact]
    public async Task Position_CannotBeDeactivatedWhileHeld_AndInactiveOrOccupiedCannotBeAssigned()
    {
        var db = Db(); var (dept, disc) = await Org(db); var svc = Svc(db);
        db.Employees.AddRange(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", DepartmentId = dept, DisciplineId = disc, IsActive = true },
                              new Employee { EmployeeId = 2, FirstName = "C", LastName = "D", DepartmentId = dept, DisciplineId = disc, IsActive = true });
        await db.SaveChangesAsync();
        await svc.CreatePositionAsync("P1", "P1", null, null, null); await svc.CreatePositionAsync("P2", "P2", null, null, null);
        var p1 = await db.Positions.SingleAsync(p => p.PositionCode == "P1"); var p2 = await db.Positions.SingleAsync(p => p.PositionCode == "P2");

        Assert.True((await svc.SetPrimaryPositionAsync(1, p1.PositionId)).Success);
        Assert.Equal("Org_Err_PositionHasHolder", (await svc.SetPositionActiveAsync(p1.PositionId, false)).ErrorKey);           // not silently closed
        Assert.Equal("Org_Err_PositionOccupied", (await svc.SetPrimaryPositionAsync(2, p1.PositionId)).ErrorKey);              // exclusive holder
        Assert.Empty(await db.EmployeeAssignments.Where(a => a.EmployeeId == 2).ToListAsync());

        await svc.SetPositionActiveAsync(p2.PositionId, false);
        Assert.Equal("Org_Err_PositionInactive", (await svc.SetPrimaryPositionAsync(2, p2.PositionId)).ErrorKey);             // inactive
        Assert.DoesNotContain((await svc.GetVacantPositionsAsync()), v => v.PositionId == p1.PositionId || v.PositionId == p2.PositionId);
    }

    [Fact]
    public async Task SetPrimaryPosition_Change_KeepsHistory_FailureKeepsCurrent_AndNeverCreatesTwoPrimaries()
    {
        var db = Db(); var (dept, disc) = await Org(db); var svc = Svc(db);
        db.Employees.AddRange(new Employee { EmployeeId = 1, FirstName = "A", LastName = "B", DepartmentId = dept, DisciplineId = disc, IsActive = true },
                              new Employee { EmployeeId = 2, FirstName = "C", LastName = "D", DepartmentId = dept, DisciplineId = disc, IsActive = true });
        await db.SaveChangesAsync();
        foreach (var c in new[] { "A", "B", "C" }) await svc.CreatePositionAsync("Pos " + c, "POS-" + c, null, null, null);
        var pos = await db.Positions.OrderBy(p => p.PositionId).ToListAsync();

        Assert.True((await svc.SetPrimaryPositionAsync(1, pos[0].PositionId)).Success);
        Assert.True((await svc.SetPrimaryPositionAsync(1, pos[1].PositionId)).Success);                 // change position
        var all = await db.EmployeeAssignments.Where(a => a.EmployeeId == 1).ToListAsync();
        Assert.Equal(2, all.Count);                                                                      // historical assignment kept
        Assert.Single(all.Where(a => a.IsActive)); Assert.Equal(pos[1].PositionId, all.Single(a => a.IsActive).PositionId);
        Assert.Contains((await svc.GetVacantPositionsAsync()), v => v.PositionId == pos[0].PositionId);  // old position is vacant again

        Assert.True((await svc.SetPrimaryPositionAsync(2, pos[2].PositionId)).Success);
        Assert.Equal("Org_Err_PositionOccupied", (await svc.SetPrimaryPositionAsync(1, pos[2].PositionId)).ErrorKey);   // target held by someone else
        Assert.Equal(pos[1].PositionId, (await db.EmployeeAssignments.SingleAsync(a => a.EmployeeId == 1 && a.IsActive)).PositionId);   // current untouched

        Assert.True((await svc.SetPrimaryPositionAsync(1, null)).Success);                               // clear
        Assert.Empty(await db.EmployeeAssignments.Where(a => a.EmployeeId == 1 && a.IsActive).ToListAsync());
        Assert.True((await svc.SetPrimaryPositionAsync(1, pos[2].PositionId)).Success == false);          // still held by employee 2
    }

    // ------------------------------------------------------------------ Employee UI → canonical assignment service

    private static EmployeeController NewEmployeeController(PEMSDbContext db, params string[] roles)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db); services.AddLogging();
        services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<PEMSDbContext>();
        var sp = services.BuildServiceProvider();
        var c = new EmployeeController(db, sp.GetRequiredService<UserManager<ApplicationUser>>(), sp.GetRequiredService<RoleManager<ApplicationRole>>(), null!, null!,
            new UserRoleAssignmentSynchronizer(db), Svc(db)).WithTempData();
        c.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test"));
        return c;
    }

    [Fact]
    public async Task EmployeeCreate_AsSystemAdmin_AssignsCanonicalPrimaryPosition_AndSyncsDisplayTitle()
    {
        var db = Db(); var (dept, disc) = await Org(db); await Svc(db).CreatePositionAsync("Mechanical Engineer", "ENG-MEC", dept, null, null);
        var pos = await db.Positions.SingleAsync();
        var c = NewEmployeeController(db, SystemRoles.SystemAdmin);

        await c.Create("", "Ali", "Ahmadi", null, null, null, dept, disc, false, null, null, null, null, null, null, null, null, pos.PositionId);

        var emp = await db.Employees.SingleAsync();
        var asg = await db.EmployeeAssignments.SingleAsync();
        Assert.Equal(pos.PositionId, asg.PositionId); Assert.True(asg.IsPrimary); Assert.True(asg.IsActive); Assert.Equal(emp.EmployeeId, asg.EmployeeId);
        Assert.Equal("Mechanical Engineer", emp.Position);                                              // legacy text only mirrors the title
        Assert.Equal(disc, emp.DisciplineId);
    }

    [Fact]
    public async Task EmployeeCreateAndEdit_AsEngineeringManager_CannotAssignOrChangePosition_ServerSide()
    {
        var db = Db(); var (dept, disc) = await Org(db); await Svc(db).CreatePositionAsync("P", "P-1", null, null, null);
        var pos = await db.Positions.SingleAsync();
        var c = NewEmployeeController(db, SystemRoles.EngineeringManager);

        await c.Create("", "Eve", "Smith", null, null, null, dept, disc, false, null, null, null, null, null, null, null, null, pos.PositionId);
        Assert.Single(db.Employees);                                       // employee itself is saved (personnel data is theirs to manage)
        Assert.Empty(db.EmployeeAssignments);                              // the position is not
        Assert.NotNull(c.TempData["Warning"]);

        var emp = await db.Employees.SingleAsync();
        await c.Edit(emp.EmployeeId, "", "Eve", "Smith", null, null, null, dept, disc, null, null, null, null, null, null, false, pos.PositionId, true);   // forged positionSubmitted
        Assert.Empty(db.EmployeeAssignments);
    }

    [Fact]
    public async Task EmployeeEdit_AsSystemAdmin_ChangesAndClearsPosition_AndDoesNotWipeLegacyTitle()
    {
        var db = Db(); var (dept, disc) = await Org(db); var svc = Svc(db);
        await svc.CreatePositionAsync("Pos One", "P-1", null, null, null); await svc.CreatePositionAsync("Pos Two", "P-2", null, null, null);
        var p1 = await db.Positions.SingleAsync(p => p.PositionCode == "P-1"); var p2 = await db.Positions.SingleAsync(p => p.PositionCode == "P-2");
        db.Employees.Add(new Employee { EmployeeId = 7, FirstName = "Z", LastName = "Y", DepartmentId = dept, DisciplineId = disc, IsActive = true, Position = "Legacy title" });
        await db.SaveChangesAsync();
        var c = NewEmployeeController(db, SystemRoles.SystemAdmin);

        // form without position fields (old/other client): legacy text and assignment untouched
        await c.Edit(7, "", "Z", "Y", null, null, null, dept, disc, null, null, null, null, null, null);
        Assert.Equal("Legacy title", (await db.Employees.AsNoTracking().SingleAsync()).Position);
        Assert.Empty(db.EmployeeAssignments);

        await c.Edit(7, "", "Z", "Y", null, null, null, dept, disc, null, null, null, null, null, null, false, p1.PositionId, true);
        Assert.Equal(p1.PositionId, (await db.EmployeeAssignments.SingleAsync(a => a.IsActive)).PositionId);
        Assert.Equal("Pos One", (await db.Employees.AsNoTracking().SingleAsync()).Position);

        await c.Edit(7, "", "Z", "Y", null, null, null, dept, disc, null, null, null, null, null, null, false, p2.PositionId, true);
        Assert.Equal(p2.PositionId, (await db.EmployeeAssignments.SingleAsync(a => a.IsActive)).PositionId);
        Assert.Equal(2, await db.EmployeeAssignments.CountAsync());                                    // history kept

        await c.Edit(7, "", "Z", "Y", null, null, null, dept, disc, null, null, null, null, null, null, false, null, true);   // clear
        Assert.Empty(await db.EmployeeAssignments.Where(a => a.IsActive).ToListAsync());
    }

    [Fact]
    public async Task EmployeeDetailsAndIndex_ExposePrimaryPositionForTheForms()
    {
        var db = Db(); var (dept, disc) = await Org(db); await Svc(db).CreatePositionAsync("Planner", "PLN", null, null, null);
        var pos = await db.Positions.SingleAsync();
        db.Employees.Add(new Employee { EmployeeId = 9, FirstName = "P", LastName = "Q", DepartmentId = dept, DisciplineId = disc, IsActive = true }); await db.SaveChangesAsync();
        await Svc(db).SetPrimaryPositionAsync(9, pos.PositionId);
        var c = NewEmployeeController(db, SystemRoles.SystemAdmin);

        await c.Index(null, null, null);
        var primaries = (Dictionary<int, (int PositionId, string Label)>)c.ViewBag.PrimaryPositions;
        Assert.Equal(pos.PositionId, primaries[9].PositionId); Assert.True((bool)c.ViewBag.CanAssignPosition);
        Assert.DoesNotContain(((List<(int PositionId, string Label)>)c.ViewBag.VacantPositions), v => v.PositionId == pos.PositionId);   // occupied: not offered to others
    }

    // ------------------------------------------------------------------ authorization + discoverability

    [Fact]
    public void PositionActions_AreSystemAdminOnly_AndMutationsAreAntiForgeryProtected()
    {
        var t = typeof(OrganizationController);
        var cls = (AuthorizeAttribute)Attribute.GetCustomAttribute(t, typeof(AuthorizeAttribute))!;
        Assert.Equal(SystemRoles.SystemAdmin, cls.Roles);
        foreach (var name in new[] { "CreatePosition", "EditPosition", "SetPositionActive" })
            Assert.NotNull(t.GetMethod(name)!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false).SingleOrDefault());
        Assert.NotNull(t.GetMethod("Positions"));
    }

    [Fact]
    public void Sidebar_HasOneEntryEach_ForDisciplineMasterData_Positions_OrganizationStructure()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web/Views/Shared/_SidebarMenu.cshtml"))) dir = dir.Parent;
        var side = File.ReadAllText(Path.Combine(dir!.FullName, "PEMS.Web/Views/Shared/_SidebarMenu.cshtml"));
        // Discipline Master Data: once in the full Administration section (admin) and once in the limited, permission-only section
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(side, "asp-controller=\"Discipline\" asp-action=\"Manage\"").Count);
        var limitedStart = side.IndexOf("sec-admin-org", StringComparison.Ordinal);
        var limited = side.Substring(limitedStart, side.IndexOf("@if (canSeeAdminCore)", limitedStart, StringComparison.Ordinal) - limitedStart);
        Assert.Contains("!canSeeAdminCore && canSeeDisciplineMaster", side);
        Assert.DoesNotContain("Positions", limited);                 // Position management is never exposed through the limited section
        Assert.DoesNotContain("asp-controller=\"Organization\"", limited);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(side, "asp-controller=\"Organization\" asp-action=\"Positions\""));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(side, "asp-controller=\"Organization\" asp-action=\"Index\""));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(side, "asp-controller=\"Employee\" asp-action=\"Index\""));
        // the position selector is wired into create, edit (per row) and the details edit modal - no free-text position inputs remain
        foreach (var f in new[] { "Index", "Details" })
        {
            var view = File.ReadAllText(Path.Combine(dir.FullName, $"PEMS.Web/Views/Employee/{f}.cshtml"));
            Assert.Contains("_PositionSelect", view);
            Assert.DoesNotContain("name=\"position\"", view);
        }
    }

    [Fact]
    public async Task DisciplineManage_SearchAndStatusFilters_AndInactiveDisciplineIsNotOffered()
    {
        var db = Db();
        db.Disciplines.AddRange(new Discipline { Code = "EL", Name = "Electrical", ImportAliases = "Elec", IsActive = true }, new Discipline { Code = "OLD", Name = "Retired", IsActive = false });
        await db.SaveChangesAsync();
        var c = new DisciplineController(null!, db, new FakeCurrentUserService { Roles = new List<string> { SystemRoles.SystemAdmin } }, new PermissionService(db, new AccessControlService(db))).WithTempData();
        var all = (List<PEMS.Application.Interfaces.DisciplineRow>)((ViewResult)await c.Manage(null, null)).Model!;
        Assert.Equal(2, all.Count);
        Assert.Single((List<PEMS.Application.Interfaces.DisciplineRow>)((ViewResult)await c.Manage("elec", null)).Model!);
        Assert.Equal("OLD", ((List<PEMS.Application.Interfaces.DisciplineRow>)((ViewResult)await c.Manage(null, "inactive")).Model!).Single().Code);
    }

    [Fact]
    public async Task EmployeeEdit_RejectedPositionChange_KeepsCurrentAssignmentAndLegacyText_AndWarns()
    {
        var db = Db(); var (dept, disc) = await Org(db); var svc = Svc(db);
        await svc.CreatePositionAsync("Mine", "P-MINE", null, null, null); await svc.CreatePositionAsync("Taken", "P-TAKEN", null, null, null); await svc.CreatePositionAsync("Retired", "P-OLD", null, null, null);
        var mine = await db.Positions.SingleAsync(p => p.PositionCode == "P-MINE"); var taken = await db.Positions.SingleAsync(p => p.PositionCode == "P-TAKEN"); var old = await db.Positions.SingleAsync(p => p.PositionCode == "P-OLD");
        db.Employees.AddRange(new Employee { EmployeeId = 1, FirstName = "A", LastName = "A", DepartmentId = dept, DisciplineId = disc, IsActive = true },
                              new Employee { EmployeeId = 2, FirstName = "B", LastName = "B", DepartmentId = dept, DisciplineId = disc, IsActive = true });
        await db.SaveChangesAsync();
        await svc.SetPrimaryPositionAsync(1, mine.PositionId); await svc.SetPrimaryPositionAsync(2, taken.PositionId);
        await svc.SetPositionActiveAsync(old.PositionId, false);
        var c = NewEmployeeController(db, SystemRoles.SystemAdmin);
        var before = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeId == 1);

        await c.Edit(1, "", "A", "A", null, null, null, dept, disc, null, null, null, null, null, null, false, taken.PositionId, true);   // occupied
        Assert.Equal(mine.PositionId, (await db.EmployeeAssignments.SingleAsync(a => a.EmployeeId == 1 && a.IsActive)).PositionId);
        Assert.NotNull(c.TempData["Warning"]);
        await c.Edit(1, "", "A", "A", null, null, null, dept, disc, null, null, null, null, null, null, false, old.PositionId, true);     // inactive
        Assert.Equal(mine.PositionId, (await db.EmployeeAssignments.SingleAsync(a => a.EmployeeId == 1 && a.IsActive)).PositionId);
        Assert.Equal(before.Position, (await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeId == 1)).Position);                 // legacy text consistent with the kept assignment
        await c.Edit(1, "", "A", "A", null, null, null, dept, disc, null, null, null, null, null, null, false, 99999, true);              // unknown
        Assert.Equal(mine.PositionId, (await db.EmployeeAssignments.SingleAsync(a => a.EmployeeId == 1 && a.IsActive)).PositionId);
    }

    [Fact]
    public async Task PositionsAndDisciplines_NeverGrantApplicationPermissions()
    {
        var db = Db(); var (dept, disc) = await Org(db); var svc = Svc(db);
        db.Employees.Add(new Employee { EmployeeId = 1, FirstName = "A", LastName = "A", DepartmentId = dept, DisciplineId = disc, IsActive = true }); await db.SaveChangesAsync();
        var roles = await db.UserRoles.CountAsync(); var assignments = await db.RoleAssignments.CountAsync(); var grants = await db.RolePermissionGrants.CountAsync();
        await svc.CreatePositionAsync("Head of Everything", "HEAD", dept, null, null);
        await svc.SetPrimaryPositionAsync(1, (await db.Positions.SingleAsync()).PositionId);
        await new DisciplineMasterService(db).CreateAsync("ADM", "Administration", null, null, null);
        Assert.Equal(roles, await db.UserRoles.CountAsync()); Assert.Equal(assignments, await db.RoleAssignments.CountAsync()); Assert.Equal(grants, await db.RolePermissionGrants.CountAsync());
    }
}
