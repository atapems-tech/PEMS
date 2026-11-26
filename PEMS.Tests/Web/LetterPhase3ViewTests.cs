using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Web;

/// <summary>
/// Phase 3 مکاتبات - صفحات و Controller: پاسخ با قالب و نشانه‌ها، پیش‌نمایش/راهنمای نشانه‌ها در مدیریت قالب،
/// دکمه‌ی اجرای دستی یادآوری فقط برای SystemAdmin، و کلیدهای Localization.
/// </summary>
public class LetterPhase3ViewTests
{
    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId { get; init; } = 5;
        public string? UserName { get; init; } = "sara";
        public string? FullName { get; init; } = "Sara <Admin>";
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; init; } = new[] { "Staff" };
    }

    private sealed class FakeTemplates : ILetterTemplateService
    {
        public List<LetterTemplateDto> Items { get; } = new();
        public Task<List<LetterTemplateDto>> ListAsync(string? letterType, bool includeInactive)
            => Task.FromResult(Items.Where(x => (includeInactive || x.IsActive) && (letterType == null || x.LetterType == null || x.LetterType == letterType)).ToList());
        public Task<LetterTemplateDto?> GetAsync(int id) => Task.FromResult(Items.FirstOrDefault(x => x.LetterTemplateId == id));
        public Task<(bool Success, string? Error, int TemplateId)> SaveAsync(LetterTemplateDto template) => throw new NotSupportedException();
        public Task<bool> SetActiveAsync(int id, bool active) => throw new NotSupportedException();
    }

    private sealed class FakeReminders : ILetterReminderService
    {
        public int Calls { get; private set; }
        public Task<LetterReminderResult> SendDueRemindersAsync(DateTime? now = null)
        {
            Calls++;
            return Task.FromResult(new LetterReminderResult { ReferralReminders = 2, ResponseReminders = 1, SkippedDuplicates = 4 });
        }
    }

    private static LetterController NewController(FakeLetterService letters, FakeTemplates? templates = null, ILetterReminderService? reminders = null,
        ICurrentUserService? user = null)
    {
        var c = new LetterController(letters, null!, PemsDbContextFactory.CreateInMemory("LetterCtl_" + Guid.NewGuid()), user ?? new FakeUser(), new FakePermissionService(), null!, null!, null!, null!, null!, null!, templates, reminders);
        return c.WithTempData();
    }

    private static LetterDetailDto Parent(bool responseRequired = false, DateTime? due = null, bool overdue = false) => new()
    {
        Letter = new LetterDto
        {
            LetterId = 40, LetterType = "Incoming", LetterNumber = "IN-40", Subject = "Please advise", Sender = "ACME Ltd",
            LetterDate = new DateTime(2026, 3, 1), ProjectName = "Alpha", ResponseRequired = responseRequired, ResponseDueDate = due, ResponseOverdue = overdue
        }
    };

    private static LetterTemplateDto Tpl(int id, string? type, string subject = "Re: {{ParentSubject}}", string body = "<p>Dear {{Counterparty}}, ref {{ParentNumber}} of {{ParentDate}} ({{ParentProject}}) from {{UserName}} on {{Date}} - {{Unknown}}</p>")
        => new() { LetterTemplateId = id, Name = "Tpl" + id, LetterType = type, Subject = subject, Body = body, Priority = "High", ConfidentialityLevel = "Internal", IsActive = true };

    // ------------------------------------------------------------------ Reply behaviour

    [Fact]
    public void ReplyGet_OpensTheComposer_WithTheParentAndTheChosenTemplate()
    {
        var c = NewController(new FakeLetterService { DetailProvider = _ => Parent() });

        var redirect = Assert.IsType<RedirectToActionResult>(c.Reply(40, templateId: 7));

        Assert.Equal("Compose", redirect.ActionName);
        Assert.Equal("LetterCompose", redirect.ControllerName);
        Assert.Equal(40, redirect.RouteValues!["replyTo"]);
        Assert.Equal(7, redirect.RouteValues["templateId"]);
    }

    // ------------------------------------------------------------------ Reply view

    private static async Task<string> RenderReply(Action<LetterController> setup)
    {
        var c = NewController(new FakeLetterService());
        c.ViewBag.Thread = new List<ThreadMessageDto>();
        c.ViewBag.ParentLetterId = 40;
        c.ViewBag.DefaultSubject = "Re: Please advise";
        c.ViewBag.DefaultReceiver = "ACME Ltd";
        c.ViewBag.Templates = new List<LetterTemplateDto> { Tpl(1, "Outgoing"), Tpl(2, null) };
        setup(c);
        var result = new ViewResult { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(c.ViewData) { Model = new object() } };
        return await RazorViewRenderer.RenderAsync(c, result, "~/Views/Letter/Reply.cshtml");
    }

    [Fact]
    public async Task ReplyView_OffersATemplateGetForm_PrefillsTheBody_AndKeepsTheChosenOneSelected()
    {
        var html = await RenderReply(c =>
        {
            c.ViewBag.Prefill = new LetterTemplateDto { Subject = "Re: Please advise", Body = "<p>Dear ACME Ltd</p>" };
            c.ViewBag.SelectedTemplateId = 2;
        });

        Assert.Contains("id=\"replyTemplate\"", html);
        var before = html[..html.IndexOf("id=\"replyTemplate\"", StringComparison.Ordinal)];
        var form = before[before.LastIndexOf("<form", StringComparison.Ordinal)..];
        Assert.Contains("method=\"get\"", form);
        Assert.Contains("name=\"id\" value=\"40\"", form);
        Assert.Matches(new Regex("<option value=\"2\" selected"), html);
        Assert.Contains("Dear ACME Ltd", html);
        Assert.Contains("value=\"Re: Please advise\"", html);
    }

    [Fact]
    public async Task ReplyView_ShowsTheParentResponseDeadline_WithTextAndIcon_AndFlagsOverdue()
    {
        var pending = await RenderReply(c => c.ViewBag.ParentLetter = Parent(true, new DateTime(2026, 4, 1)).Letter);
        Assert.Contains("This letter needs a response by", pending);
        Assert.Contains("2026-04-01", pending);
        Assert.Contains("bi-reply", pending);
        Assert.DoesNotContain("alert-danger", pending);

        var overdue = await RenderReply(c => c.ViewBag.ParentLetter = Parent(true, new DateTime(2026, 3, 1), overdue: true).Letter);
        Assert.Contains("alert-danger", overdue);
        Assert.Contains("(overdue)", overdue);
        Assert.Contains("bi-exclamation-triangle", overdue);

        var none = await RenderReply(c => c.ViewBag.ParentLetter = Parent().Letter);
        Assert.DoesNotContain("This letter needs a response by", none);
    }

    [Fact]
    public async Task ReplyView_WithoutTemplates_HidesThePicker()
    {
        var html = await RenderReply(c => c.ViewBag.Templates = new List<LetterTemplateDto>());

        Assert.DoesNotContain("id=\"replyTemplate\"", html);
    }

    // ------------------------------------------------------------------ template manager views

    private static LetterTemplateController NewTemplateController(FakeTemplates t) => new(t)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task TemplatePreview_ShowsSampleValues_NeverRealData_AndEncodesThem()
    {
        var t = new FakeTemplates();
        t.Items.Add(Tpl(9, "Outgoing", subject: "Re {{ParentSubject}} - {{Bogus}}", body: "<p>{{UserName}} / {{Counterparty}} / {{ParentNumber}}</p>"));
        var c = NewTemplateController(t);

        var view = Assert.IsType<ViewResult>(await c.Preview(9));
        var html = await RazorViewRenderer.RenderAsync(c, view, "~/Views/LetterTemplate/Preview.cshtml");

        Assert.Contains("Re Sample subject - {{Bogus}}", html);
        Assert.Contains("Sample User / Sample Company / LTR-0001", html);
        Assert.Contains("Shown with sample values", html);
        Assert.IsType<NotFoundResult>(await c.Preview(12345));
    }


    // ------------------------------------------------------------------ manual reminder run

    // SCRUM-716: the manual run is the operation Letter.SendReminders
    private static PEMS.Web.Services.LetterOperations RemindersOperations(ILetterReminderService? reminders, params string[] roles)
        => new(PemsDbContextFactory.CreateInMemory("LetterOps_" + Guid.NewGuid()), new FakeLetterService(), null!, null!, null!, new FakeUser { Roles = roles }, new FakePermissionService(), null!, null, null, reminders);

    [Fact]
    public async Task SendReminders_IsForbiddenForNonAdmins_AndNeverRunsTheJob()
    {
        var reminders = new FakeReminders();
        var ops = RemindersOperations(reminders, "Staff");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ops.SendRemindersAsync());
        Assert.Equal(0, reminders.Calls);
    }

    [Fact]
    public async Task SendReminders_RunsOnceForSystemAdmin_ThenReportsWhatWasSent()
    {
        var reminders = new FakeReminders();
        var ops = RemindersOperations(reminders, "SystemAdmin");

        var result = await ops.SendRemindersAsync();

        Assert.True(result.Success);
        Assert.Equal(1, reminders.Calls);
        Assert.Contains("2 referral", result.Error);
        Assert.Contains("1 response", result.Error);
        Assert.Contains("4 already sent today", result.Error);
    }

    [Fact]
    public async Task SendReminders_WithoutTheService_IsRefused()
    {
        var result = await RemindersOperations(null, "SystemAdmin").SendRemindersAsync();

        Assert.False(result.Success);
    }

    private static async Task<string> RenderDashboard(bool canRun)
    {
        var c = NewController(new FakeLetterService());
        c.ViewBag.CanReview = true;
        c.ViewBag.CanRunReminders = canRun;
        var result = new ViewResult { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(c.ViewData) { Model = new LetterWorkspaceDashboardDto() } };
        return await RazorViewRenderer.RenderAsync(c, result, "~/Views/Letter/Dashboard.cshtml");
    }

    [Fact]
    public async Task Dashboard_ShowsTheRunRemindersButton_OnlyWhenAllowed()
    {
        Assert.Contains("Send overdue reminders now", await RenderDashboard(true));
        Assert.DoesNotContain("Send overdue reminders now", await RenderDashboard(false));
    }

    // ------------------------------------------------------------------ localization

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

}
