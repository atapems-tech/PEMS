using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Areas.Admin.Models;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 wave 4a (admin and base data): Users, Roles, Departments, Disciplines, Employees, the Audit Log, Security
/// Events and the Authorization Audit are built from the PEMS.UI list-page component. Each page keeps its columns, filters,
/// row actions, confirmations and modals; the admin hero, content cards, filter rows and inline column script are gone.
/// </summary>
public class AdminBaseDataListPagesTests
{
    private static readonly string[] Pages =
    {
        "Areas/Admin/Views/Users", "Areas/Admin/Views/Roles", "Views/Department", "Views/Discipline", "Views/Employee",
        "Areas/Admin/Views/AuditLog", "Areas/Admin/Views/SecurityEvents", "Areas/Admin/Views/AuthorizationAudit",
    };

    private static Task<string> Render<TController>(string view, object model, Action<ViewDataDictionary>? data = null) where TController : Controller
    {
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));
        data?.Invoke(controller.ViewData);
        var result = new ViewResult { ViewData = new ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, view);
    }

    private static PagedResult<T> Page<T>(params T[] items) => new() { Items = items.ToList(), Page = 1, PageSize = 25, TotalCount = items.Length };

    private static void AssertBuiltFromTheComponent(string html, string gridKey, int rows)
    {
        Assert.Contains("class=\"lp-page\"", html);
        Assert.Contains("class=\"lp-toolbar\"", html);
        Assert.Equal(rows, Regex.Matches(html, "<tr class=\"lp-row").Count);
        Assert.Matches($"<table class=\"[^\"]*lp-table[^\"]*\" data-grid-key=\"{gridKey}\"", html);
        Assert.Contains($"data-lp-reset-columns=\"{gridKey}\"", html);
        Assert.DoesNotContain("content-card", html);
        Assert.DoesNotContain("admin-hero", html);
        Assert.DoesNotContain("PemsDataGrid.initColumnResize", html);
        Assert.DoesNotContain("class=\"badge ", html);
    }

    private static string Chips(string html) => Regex.Match(html, "<div class=\"lp-chips\">(.*?)</div>", RegexOptions.Singleline).Value;

    [Fact]
    public async Task Users_SearchIsAChip_StatusesArePills_AndTheToggleAsksInTheLanguage()
    {
        var html = await Render<UsersController>("/Areas/Admin/Views/Users/Index.cshtml",
            Page(new UserListItemViewModel { Id = 7, UserName = "ali", FullName = "Ali R", IsActive = true, LockedOut = true, Roles = new List<string> { "SystemAdmin" } }),
            v => v["SearchTerm"] = "ali");

        AssertBuiltFromTheComponent(html, "AdminUsers", 1);
        Assert.Single(Regex.Matches(Chips(html), "class=\"pems-filter-chip\""));
        Assert.Contains("vs-pill--success", html);                                       // active
        Assert.Contains("vs-pill--danger", html);                                        // locked
        Assert.Contains("data-confirm=\"Deactivate user ali?\"", html);
        Assert.Contains("bi-key", html);                                                 // reset administrator (another admin)
        Assert.Contains("lp-compose", html);
    }

    [Fact]
    public async Task Roles_KeepTheAuthorizationStatus_AndDeleteOnlyForCustomRoles()
    {
        var html = await Render<RolesController>("/Areas/Admin/Views/Roles/Index.cshtml", new List<RoleListItemViewModel>
        {
            new() { Id = 1, Name = "SystemAdmin", IsSystemRole = true, PermissionCount = -1, UserCount = 1 },
            new() { Id = 2, Name = "Custom.Team", IsSystemRole = false, PermissionCount = 0 },
        });

        AssertBuiltFromTheComponent(html, "AdminRoles", 2);
        Assert.Contains("bi-infinity", html);
        Assert.Contains("bi-exclamation-triangle", html);                                // not configured
        Assert.Single(Regex.Matches(html, "bi-trash"));
    }

    [Fact]
    public async Task Departments_And_Disciplines_UseTheListFrame()
    {
        var dept = await Render<PEMS.Web.Controllers.DepartmentController>("/Views/Department/Index.cshtml",
            new List<Department> { new() { DepartmentId = 3, Code = "ENG", Name = "Engineering", IsActive = true } },
            v => v["EmployeeCounts"] = new Dictionary<int, int> { [3] = 12 });
        AssertBuiltFromTheComponent(dept, "Department", 1);
        Assert.Contains("id=\"createDeptModal\"", dept);
        Assert.Contains("id=\"editDeptModal_3\"", dept);
        Assert.Matches("<button[^>]*data-bs-target=\"#createDeptModal\"", dept);

        var disc = await Render<DisciplineController>("/Views/Discipline/Index.cshtml",
            new List<DisciplineSummaryDto> { new() { DisciplineId = 1, Code = "MECH", Name = "Mechanical", OverdueTasksCount = 2 } },
            v => v["CanManageMaster"] = true);
        AssertBuiltFromTheComponent(disc, "Discipline", 1);
        Assert.Contains("bi-sliders", disc);                                              // manage disciplines tool
        Assert.Contains("vs-pill vs-pill--danger", disc);
    }

    [Fact]
    public async Task Logs_FilterRowsMoveIntoTheDropdown_AndEachActiveFilterIsAChip()
    {
        var audit = await Render<AuditLogController>("/Areas/Admin/Views/AuditLog/Index.cshtml",
            Page(new AuditLog { Timestamp = new DateTime(2026, 10, 31), UserName = "ali", EntityName = "Vendor", Action = "Delete" }),
            v => { v["EntityNames"] = new List<string> { "Vendor" }; v["SelectedEntityName"] = "Vendor"; v["SelectedUserName"] = "ali"; });
        AssertBuiltFromTheComponent(audit, "AuditLogs", 1);
        Assert.Equal(2, Regex.Matches(Chips(audit), "class=\"pems-filter-chip\"").Count);
        Assert.Contains("vs-pill--danger", audit);

        var security = await Render<SecurityEventsController>("/Areas/Admin/Views/SecurityEvents/Index.cshtml",
            Page(new SecurityEventDto { Method = RecoveryMethods.RecoveryCode, Result = RecoveryResults.Failure, FailureReason = "expired", OccurredAtUtc = new DateTime(2026, 10, 31) }),
            v => { v["Method"] = null; v["Result"] = RecoveryResults.Failure; });
        AssertBuiltFromTheComponent(security, "SecurityEvents", 1);
        Assert.Single(Regex.Matches(Chips(security), "class=\"pems-filter-chip\""));
        Assert.Contains("expired", security);

        var authz = await Render<AuthorizationAuditController>("/Areas/Admin/Views/AuthorizationAudit/Index.cshtml",
            Page(new AuthorizationAuditLog { Timestamp = new DateTime(2026, 10, 31), UserId = 5, ResourceName = "Vendor", ActionName = "View", Decision = "Deny" }),
            v =>
            {
                v["ResourceNames"] = new List<string> { "Vendor" }; v["Decisions"] = new List<string> { "Allow", "Deny" };
                v["UserNames"] = new Dictionary<int, string> { [5] = "ali" };
                v["SelectedUserId"] = 5; v["SelectedResourceName"] = "Vendor"; v["SelectedDecision"] = "Deny"; v["SelectedFromDate"] = "2026-10-01";
            });
        AssertBuiltFromTheComponent(authz, "AuthorizationAudit", 1);
        Assert.Matches("<span class=\"lp-badge\">4<", authz);
        Assert.Contains("ali", Chips(authz));                                             // the user opened from another page is a chip
    }

    private static string RepoWeb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "PEMS.Web");
    }

    [Fact]
    public void EveryWave4aPage_UsesTheComponentAndItsAssets()
    {
        foreach (var page in Pages)
        {
            var view = File.ReadAllText(Path.Combine(RepoWeb(), page.Replace('/', Path.DirectorySeparatorChar), "Index.cshtml"));
            Assert.Contains("<pems-list-page", view);
            Assert.Contains("_content/PEMS.UI/pems-list.css", view);
            Assert.Contains("_content/PEMS.UI/pems-list.js", view);
            Assert.DoesNotContain("PemsDataGrid.initColumnResize", view);
            Assert.DoesNotMatch("class=\"btn btn-", view);
        }
        // the employee page keeps its department / discipline filters and its create and edit modals
        var employee = File.ReadAllText(Path.Combine(RepoWeb(), "Views", "Employee", "Index.cshtml"));
        Assert.Contains("<pems-list-filter-select id=\"emFilterDepartment\"", employee);
        Assert.Contains("<pems-list-new modal=\"createEmpModal\"", employee);
        Assert.Contains("id=\"editEmpModal_@e.EmployeeId\"", employee);
    }
}
