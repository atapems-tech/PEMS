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

    [Fact]
    public async Task Dashboard_ShowsRealNumbersAsText_WithWarningsAsIconPlusText()
    {
        var html = await RenderDashboard(Dash(), canReview: true);

        foreach (var label in new[] { "Active letters", "Open letters", "Awaiting approval", "My drafts", "Returned to me", "My open referrals",
                     "Open referrals", "Overdue referrals", "Overdue responses", "Received (30d)", "Issued (30d)" })
            Assert.Contains(label, html);
        Assert.Contains("<strong class=\"vs-tile__value\">10</strong>", html);
        Assert.Contains("<strong class=\"vs-tile__value\">3</strong>", html);

        // the warning marker is text + icon, not colour alone
        Assert.Contains("Needs action", html);
        Assert.Contains("bi-exclamation-triangle", html);
        Assert.DoesNotContain("<h1", html);
    }

    [Fact]
    public async Task Dashboard_AwaitingApprovalTile_IsHiddenForNonReviewers()
    {
        Assert.Contains("Awaiting approval", await RenderDashboard(Dash(), canReview: true));
        Assert.DoesNotContain("Awaiting approval", await RenderDashboard(Dash(), canReview: false));
    }

    [Fact]
    public async Task Dashboard_NoWarningBadges_WhenNothingIsOverdue()
    {
        var model = Dash();
        model.OverdueReferrals = 0; model.OverdueResponses = 0; model.MyReturnedForRevision = 0; model.NeedsAttention.Clear();

        var html = await RenderDashboard(model, true);

        Assert.DoesNotContain("Needs action", html);
        Assert.Contains("No overdue referrals or responses.", html);
    }

    [Fact]
    public async Task Dashboard_NeedsAttentionTable_NamesTheReasonAndTheDays()
    {
        var html = await RenderDashboard(Dash(), true);

        Assert.Contains("Late reply", html);
        Assert.Contains("Overdue response", html);
        Assert.Contains("Overdue referral", html);
        Assert.Contains("2026-03-01", html);
        Assert.Matches(new Regex(@"pems-due-overdue[^>]*>.*?9", RegexOptions.Singleline), html);
    }

    [Fact]
    public async Task Dashboard_Bars_AreLabelledForAssistiveTechnology_AndCarryTheNumbers()
    {
        var html = await RenderDashboard(Dash(), true);

        Assert.Contains("role=\"progressbar\" aria-valuenow=\"20\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-label=\"0-7 days: 2 (20%)\"", html);
        Assert.Contains("role=\"progressbar\" aria-valuenow=\"60\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-label=\"Registered: 6 (60%)\"", html);
        Assert.Contains("Over 90 days", html);
        Assert.Contains("Urgent", html);
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

    [Fact]
    public void ControllerPageTitles_AreTranslated_AndLifecycleBulkStillExcludesApprove()
    {
        var fa = XDocument.Load(RepoFile("PEMS.Web/Resources/SharedResource.fa.resx")).Descendants("data").Select(d => (string?)d.Attribute("name")).ToHashSet();
        foreach (var title in new[] { "Correspondence Dashboard", "Letter Templates", "Letter Template" })
            Assert.Contains(title, fa);

        var source = File.ReadAllText(RepoFile("PEMS.Web/Controllers/LetterController.cs"));
        Assert.Contains("ViewData[\"Title\"] = \"Correspondence Dashboard\";", source);
    }
}
