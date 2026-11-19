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
        "Areas/Admin/Views/Users", "Views/Department", "Views/Discipline", "Views/Employee",
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
        Assert.Contains("data-pems-confirm=\"Deactivate user ali?\"", html);
        Assert.Contains("bi-key", html);                                                 // reset administrator (another admin)
        Assert.Contains("lp-compose", html);
    }

    [Fact]
    public async Task Departments_And_Disciplines_UseTheListFrame()
    {
        var dept = await Render<PEMS.Web.Controllers.DepartmentController>("/Views/Department/Index.cshtml",
            new List<Department> { new() { DepartmentId = 3, Code = "ENG", Name = "Engineering", IsActive = true } },
            v => v["EmployeeCounts"] = new Dictionary<int, int> { [3] = 12 });
        AssertBuiltFromTheComponent(dept, "Department", 1);
        Assert.Contains("id=\"createDeptModal\"", dept);
        Assert.Contains("id=\"editDeptModal\"", dept);                                  // one shared edit modal, SCRUM-500
        Assert.Matches("<button[^>]*data-bs-target=\"#editDeptModal\"[^>]*data-fill-departmentid=\"3\"", dept);
        Assert.Matches("<button[^>]*data-bs-target=\"#createDeptModal\"", dept);

        var disc = await Render<DisciplineController>("/Views/Discipline/Index.cshtml",
            new List<DisciplineSummaryDto> { new() { DisciplineId = 1, Code = "MECH", Name = "Mechanical", OverdueTasksCount = 2 } },
            v => v["CanManageMaster"] = true);
        AssertBuiltFromTheComponent(disc, "Discipline", 1);
        Assert.Contains("bi-sliders", disc);                                              // manage disciplines tool
        Assert.Contains("vs-pill vs-pill--danger", disc);
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
