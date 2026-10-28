using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-407: نقش‌های کاربر در یک فهرست چندانتخابی با جستجو (Tom Select) - همه‌ی نقش‌ها در گروه درست، نقش‌های فعلی انتخاب‌شده،
/// همان نام فیلد SelectedRoles برای binding، و کتابخانه‌ی محلی (بدون CDN) با فایل مجوز.
/// </summary>
public class UserRoleSelectTests
{
    private static readonly List<string> AllRoles = new()
    {
        "SystemAdmin", "EngineeringManager", "EngineeringSupervisor", "DocumentController", "ContractManager",
        "DisciplineEngineer", "MaintenanceEngineer", "Viewer", "MNT.Manager", "WH.Storekeeper", "QAManager"
    };

    private static Task<string> Render(UserFormViewModel model)
    {
        // رندر فقط به ViewData کنترلر نیاز دارد؛ سازنده (سرویس‌ها) استفاده نمی‌شود
        var controller = (UsersController)RuntimeHelpers.GetUninitializedObject(typeof(UsersController));
        var result = new ViewResult { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, "/Areas/Admin/Views/Users/_RoleSelect.cshtml");
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [Fact]
    public async Task Roles_AreOneSearchableMultiSelect_WithEveryRoleInItsGroup_AndCurrentRolesSelected()
    {
        var html = await Render(new UserFormViewModel { AllRoles = AllRoles, SelectedRoles = new() { "SystemAdmin", "MNT.Manager" } });

        // یک select چندانتخابی با همان نام فیلد قبلی (binding بدون تغییر) و علامت فعال‌سازی جستجو
        Assert.Single(Regex.Matches(html, "<select[^>]*name=\"SelectedRoles\""));
        Assert.Matches("<select[^>]*\\bmultiple\\b[^>]*data-pems-multiselect", html);
        Assert.DoesNotContain("type=\"checkbox\"", html);

        // هر نقش دقیقاً یک بار
        foreach (var role in AllRoles)
            Assert.Single(Regex.Matches(html, $"<option value=\"{Regex.Escape(role)}\""));

        // سه گروه به ترتیب: مسئول، کارشناس، سفارشی
        var groups = Regex.Matches(html, "<optgroup label=\"([^\"]+)\">(.*?)</optgroup>", RegexOptions.Singleline);
        Assert.Equal(3, groups.Count);
        Assert.Contains("value=\"ContractManager\"", groups[0].Groups[2].Value);
        Assert.Contains("value=\"Viewer\"", groups[1].Groups[2].Value);
        Assert.Contains("value=\"WH.Storekeeper\"", groups[2].Groups[2].Value);
        Assert.DoesNotContain("value=\"SystemAdmin\"", groups[2].Groups[2].Value);

        // فقط نقش‌های فعلی انتخاب‌شده‌اند
        var selected = Regex.Matches(html, "<option value=\"([^\"]+)\" selected=\"selected\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "SystemAdmin", "MNT.Manager" }, selected);
    }

    [Fact]
    public async Task WithoutCustomRoles_TheCustomGroupIsNotRendered()
    {
        var html = await Render(new UserFormViewModel { AllRoles = AllRoles.Take(8).ToList() });

        Assert.Equal(2, Regex.Matches(html, "<optgroup ").Count);
        Assert.DoesNotContain("selected=\"selected\"", html);
    }

    [Fact]
    public void TomSelect_IsVendoredOffline_WithItsLicence_AndBothUserPagesLoadTheLocalCopy()
    {
        var lib = RepoFile("PEMS.Web", "wwwroot", "lib", "tom-select");
        var js = File.ReadAllText(Path.Combine(lib, "tom-select.complete.min.js"));
        Assert.StartsWith("/**\n* Tom Select v2.6.2", js.Replace("\r\n", "\n"));
        Assert.DoesNotMatch("https?://", js);                                                       // nothing fetched at run time
        Assert.True(File.Exists(Path.Combine(lib, "tom-select.bootstrap5.min.css")));
        Assert.Contains("Apache License", File.ReadAllText(Path.Combine(lib, "LICENSE")));

        // SCRUM-57: the layout loads Tom Select once for every page; the user pages only add the multi-select initialiser
        var layout = File.ReadAllText(RepoFile("PEMS.Web", "Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("~/lib/tom-select/tom-select.complete.min.js", layout);
        Assert.Contains("~/lib/tom-select/tom-select.bootstrap5.min.css", layout);
        foreach (var page in new[] { "Create.cshtml", "Edit.cshtml" })
        {
            var view = File.ReadAllText(RepoFile("PEMS.Web", "Areas", "Admin", "Views", "Users", page));
            Assert.Contains("<partial name=\"_RoleSelect\"", view);
            Assert.DoesNotContain("~/lib/tom-select/", view);                                     // not loaded twice
            Assert.Contains("~/js/pems-multiselect.js", view);
            Assert.DoesNotMatch("(src|href)=\"https?://", view);
        }
    }
}
