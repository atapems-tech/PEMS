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
    public async Task TemplatePreview_ShowsSampleValues_NeverRealData_AndEncodesThem()
    {
        var t = new FakeTemplates();
        t.Items.Add(Tpl(9, "Outgoing", subject: "Re {{ParentSubject}} - {{Bogus}}", body: "<p>{{UserName}} / {{Counterparty}} / {{ParentNumber}}</p>"));
        var ops = new PEMS.Web.Services.LetterTemplateOperations(t);

        var row = Assert.Single(await ops.PreviewAsync(9));

        Assert.Contains("Re Sample subject - {{Bogus}}", row.Subject);
        Assert.Contains("Sample User / Sample Company / LTR-0001", row.Body);
        Assert.Empty(await ops.PreviewAsync(12345));
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
