using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Web;

/// <summary>
/// Phase 2 مکاتبات - View های واقعی: داشبورد، فرم ایجاد (قالب/پیش‌پر/پاسخ‌خواهی)، مدیریت قالب‌ها، و قراردادهای سیم‌کشی
/// (مجوز، POST/Antiforgery، کلیدهای Localization در هر دو زبان). وضعیت هرگز فقط با رنگ نمایش داده نمی‌شود.
/// </summary>
public class LetterPhase2ViewTests
{
    private static LetterController NewLetterController() => new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static ViewResult Result(Controller c, object model)
        => new() { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(c.ViewData) { Model = model } };

    private static object Obj(params (string Name, object? Value)[] props)
    {
        IDictionary<string, object?> bag = new System.Dynamic.ExpandoObject();
        foreach (var (name, value) in props) bag[name] = value;
        return bag;
    }

    // ------------------------------------------------------------------ dashboard

    private static LetterWorkspaceDashboardDto Dash() => new()
    {
        TotalActive = 10, OpenLetters = 6, AwaitingApproval = 3, MyDrafts = 2, MyReturnedForRevision = 1, MyOpenReferrals = 4,
        OpenReferrals = 5, OverdueReferrals = 2, OverdueResponses = 1, ReceivedLast30Days = 7, IssuedLast30Days = 3,
        ByStatus = new() { ["Registered"] = 6, ["Draft"] = 4 },
        ByType = new() { ["Incoming"] = 6, ["Outgoing"] = 4 },
        ByPriority = new() { ["Normal"] = 8, ["Urgent"] = 2 },
        Aging = new() { new() { Key = "0-7", Count = 2 }, new() { Key = "8-30", Count = 2 }, new() { Key = "31-90", Count = 1 }, new() { Key = "90+", Count = 1 } },
        NeedsAttention = new()
        {
            new() { LetterId = 1, LetterNumber = "L-1", Subject = "Late reply", Reason = "OverdueResponse", DueDate = new DateTime(2026, 3, 1), DaysOverdue = 9 },
            new() { LetterId = 2, LetterNumber = "L-2", Subject = "Late referral", Reason = "OverdueReferral", DueDate = new DateTime(2026, 3, 5), DaysOverdue = 4 }
        }
    };

    private static async Task<string> RenderDashboard(LetterWorkspaceDashboardDto model, bool canReview)
    {
        var c = NewLetterController();
        c.ViewBag.CanReview = canReview;
        return await RazorViewRenderer.RenderAsync(c, Result(c, model), "~/Views/Letter/Dashboard.cshtml");
    }

    // ------------------------------------------------------------------ create form

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        var root = Environment.GetEnvironmentVariable("PEMS_REPO_ROOT");
        if (!string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, relative))) return Path.Combine(root, relative);
        throw new FileNotFoundException("Repository file not found (set PEMS_REPO_ROOT when running from a copied output folder): " + relative);
    }

    [Fact]
    public void TemplateManagement_RequiresTheApprovePermission_AndHasNoDeleteAction()
    {
        // SCRUM-717: the list, the load, the save and the switch are operations; the floor of every one is the Approve of the module (the shipped actions are posts with the antiforgery token of the action host)
        var catalog = PEMS.Web.Definitions.OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);
        foreach (var op in new[] { "List", "One", "Preview", "Save", "SetActive" })
            Assert.Equal("Correspondence.Approve", catalog.Get("LetterTemplatePage", op)!.Permission);
    }

}
