using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Persistence.Identity;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Tests.Authorization;

/// <summary>
/// رندر Viewهای واقعی صفحه‌ی Permission Matrix و تب Effective کاربر (بدون HTTP/دیتابیس): هر دو بدون خطا
/// رندر می‌شوند، ساختار موردنیاز JavaScript (data-attributeها) وجود دارد و حالت‌های قفل/فقط‌خواندنی درست است.
/// Layout اجرا نمی‌شود، پس بلوک @section Scripts در خروجی نیست.
/// </summary>
public class PermissionViewRenderTests
{
    private static Task<string> Render(Controller c, string path, object model)
    {
        var result = new ViewResult { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(c.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(c, result, path);
    }

    // ------------------------------------------------------------------ Users/Details Effective tab
    private static UsersController UsersControllerForRender() =>
        new(null!, null!, PemsDbContextFactory.CreateInMemory(nameof(PermissionViewRenderTests)), null!, null!, null!, null!);

    private static UserWorkspaceVm Workspace(params string[] roles) => new()
    {
        UserId = 7,
        UserName = "alice",
        FullName = "Alice A",
        IsActive = true,
        RoleNames = roles.ToList(),
        RoleUserCounts = roles.ToDictionary(r => r, _ => 3),
        EffectivePermissions = new List<EffectivePermissionDto>
        {
            new() { ResourceName = "WorkOrder", ActionName = "View", Allowed = true },
            new() { ResourceName = "WorkOrder", ActionName = "Delete", Allowed = false, IsDestructive = true },
            new() { ResourceName = "EngineeringDocuments", ActionName = "ViewAllDisciplines", Allowed = true }
        },
        NonEditablePermissions = new Dictionary<string, string>
        {
            ["EngineeringDocuments.ViewAllDisciplines"] = PermissionChangeReasons.OutsideMatrix
        }
    };

    private static Regex Row(string permission) => new($"<tr data-permission=\"{Regex.Escape(permission)}\"[^>]*>");

    [Fact]
    public async Task EffectiveTab_Renders_EditableRows_InTheRightTable_AndLocksNonEditableOnes()
    {
        var html = await Render(UsersControllerForRender(), "~/Areas/Admin/Views/Users/Details.cshtml", Workspace("Engineer"));

        Assert.Contains("id=\"permAdmin\"", html);
        Assert.Contains("data-editable=\"1\"", html);
        Assert.Contains("id=\"permToolbar\"", html);
        Assert.Contains("id=\"permEnableSelected\"", html);
        Assert.DoesNotContain("id=\"permTargetRole\"", html); // single role: no picker needed

        var allowed = html[html.IndexOf("id=\"permTableAllowed\"", StringComparison.Ordinal)..html.IndexOf("id=\"permTableDenied\"", StringComparison.Ordinal)];
        var denied = html[html.IndexOf("id=\"permTableDenied\"", StringComparison.Ordinal)..];

        var view = Row("WorkOrder.View").Match(allowed).Value;
        Assert.Contains("data-allowed=\"1\"", view);
        Assert.Contains("data-locked=\"0\"", view);

        var delete = Row("WorkOrder.Delete").Match(denied).Value;
        Assert.Contains("data-allowed=\"0\"", delete);
        Assert.Contains("data-locked=\"0\"", delete);

        var locked = Row("EngineeringDocuments.ViewAllDisciplines").Match(allowed).Value;
        Assert.Contains("data-locked=\"1\"", locked);
        // locked rows get disabled controls
        var lockedRow = allowed[allowed.IndexOf(locked, StringComparison.Ordinal)..];
        lockedRow = lockedRow[..lockedRow.IndexOf("</tr>", StringComparison.Ordinal)];
        Assert.Matches("<input[^>]*class=\"[^\"]*perm-select\"[^>]*disabled=\"disabled\"", lockedRow);   // SCRUM-533: attribute order is the component's
        Assert.Matches("<input(?=[^>]*perm-toggle)(?=[^>]*checked=\"checked\")[^>]*disabled=\"disabled\"", lockedRow);
    }

    [Fact]
    public async Task EffectiveTab_WithSeveralRoles_OffersATargetRolePicker()
    {
        var html = await Render(UsersControllerForRender(), "~/Areas/Admin/Views/Users/Details.cshtml", Workspace("Engineer", "Viewer"));

        Assert.Contains("id=\"permTargetRole\"", html);
        Assert.Contains("<option value=\"Engineer\">", html);
        Assert.Contains("<option value=\"Viewer\">", html);
    }

    [Fact]
    public async Task EffectiveTab_ForSystemAdmin_IsReadOnly()
    {
        var html = await Render(UsersControllerForRender(), "~/Areas/Admin/Views/Users/Details.cshtml", Workspace(SystemRoles.SystemAdmin));

        Assert.Contains("data-editable=\"0\"", html);
        Assert.DoesNotContain("id=\"permToolbar\"", html);
        Assert.DoesNotContain("data-locked=\"0\"", html);
        Assert.Equal(3, Regex.Matches(html, "data-locked=\"1\"").Count);
    }
}
