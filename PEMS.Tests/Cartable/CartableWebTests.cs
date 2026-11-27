using System.Dynamic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Tests.Web;
using PEMS.Web.Controllers;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Tests.Cartable;

/// <summary>صفحه‌ی کارتابل (Index)، Details در پنل کناری، کنترلرها و فایل‌های JS/CSS: پوشه‌ها، درخت برچسب، سطل زباله، رشته‌ها، تنظیمات و گزارش.</summary>
public class CartableWebTests
{
    private const string Repo = "PEMS_REPO_ROOT";

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId { get; init; } = 102;
        public string? UserName { get; init; } = "u2";
        public string? FullName { get; init; } = "User 2";
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; init; } = new[] { "Staff" };
    }

    private static object Obj(params (string Name, object? Value)[] props)
    {
        IDictionary<string, object?> bag = new ExpandoObject();
        foreach (var (name, value) in props) bag[name] = value;
        return bag;
    }

    private static object NoFilters(string? status = null) => Obj(("letterType", null), ("letterNumber", null), ("subject", null), ("sender", null), ("receiver", null),
        ("projectId", null), ("disciplineId", null), ("status", status), ("fromDate", null), ("toDate", null), ("starred", false), ("priority", ""),
        ("hasAttachments", null), ("hasReferrals", null), ("overdueReferral", false), ("overdueResponse", false));

    private static LetterDto Row(int id, Action<LetterDto>? edit = null)
    {
        var l = new LetterDto
        {
            LetterId = id, LetterNumber = $"LTR-{id}", LetterType = "Internal", LetterDate = new DateTime(2026, 3, 1), Subject = $"Subject {id}", Priority = "Normal",
            ConfidentialityLevel = "Internal", Status = "Sent", CreatedDate = new DateTime(2026, 3, 1), LastActivityDate = new DateTime(2026, 3, 2), Receiver = "Sara", SenderName = "Ali"
        };
        edit?.Invoke(l);
        return l;
    }

    private sealed record RenderOptions(string? Folder = null, int? TagId = null, string ViewMode = "flat", CartableCountsDto? Cart = null, List<LetterTagNodeDto>? Tags = null,
        LetterUserSettingsRules.Settings? Settings = null, string? Status = null, string? Sort = null, bool SortDesc = true, bool CanBulkArchive = false, int Total = -1);

    private static async Task<string> Render(RenderOptions o, params LetterDto[] rows)
    {
        var c = new LetterController(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        c.ViewBag.Dashboard = new LetterDashboardDto();
        c.ViewBag.FolderCounts = new LetterFolderCountsDto();
        c.ViewBag.Projects = new List<object> { Obj(("ProjectId", 7), ("ProjectName", "Alpha")) };
        c.ViewBag.Disciplines = new List<object> { Obj(("DisciplineId", 1), ("Name", "Mech")) };
        c.ViewBag.CanBulkArchive = o.CanBulkArchive;
        c.ViewBag.Filters = NoFilters(o.Status);
        c.ViewBag.Cartable = o.Cart ?? new CartableCountsDto();
        c.ViewBag.TagTree = o.Tags ?? new List<LetterTagNodeDto>();
        c.ViewBag.UserSettings = o.Settings ?? LetterUserSettingsRules.Sanitize(null);
        c.ViewBag.Folder = o.Folder;
        c.ViewBag.TagId = o.TagId;
        c.ViewBag.ViewMode = o.ViewMode;
        c.ViewBag.Sort = o.Sort;
        c.ViewBag.SortDesc = o.SortDesc;
        c.ViewBag.CanManageOrg = true;
        var result = new ViewResult
        {
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(c.ViewData)
            { Model = new PagedResult<LetterDto> { Items = rows.ToList(), Page = 1, PageSize = 50, TotalCount = o.Total >= 0 ? o.Total : rows.Length } }
        };
        return await RazorViewRenderer.RenderAsync(c, result, "~/Views/Letter/Index.cshtml");
    }

    private static List<LetterTagNodeDto> Tags() => new()
    {
        new() { TagId = 1, Name = "Engineering", Color = "primary", Depth = 1, CanEdit = true },
        new() { TagId = 2, ParentTagId = 1, Name = "Electrical", Color = "warning", Depth = 2, CanEdit = true },
        new() { TagId = 3, Name = "Mine", Color = "danger", Depth = 1, IsPersonal = true, CanEdit = true }
    };

    private static string Root()
    {
        var root = Environment.GetEnvironmentVariable(Repo);
        if (!string.IsNullOrEmpty(root)) return root;
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PEMS.Web"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found; set PEMS_REPO_ROOT");
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "PEMS.Web" }.Concat(parts).ToArray()));

    // ------------------------------------------------------------------ Details (embedded pane + history options)
    private static async Task<string> RenderDetails(bool embed, string role = "all", string order = "asc")
    {
        var c = new LetterController(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        c.ViewBag.Departments = new List<object>();
        c.ViewBag.AllTags = new List<TagDto>();
        c.ViewBag.Timeline = new List<LetterTimelineEventDto> { new() { EventDate = new DateTime(2026, 3, 1), EventType = "Created", Description = "created", ActorEmployeeId = 3 } };
        c.ViewBag.Thread = new List<ThreadMessageDto>();
        c.ViewBag.CanEditReturned = false; c.ViewBag.CanEditDraft = false; c.ViewBag.CanReview = false;
        c.ViewBag.Embed = embed; c.ViewBag.HistoryRole = role; c.ViewBag.HistoryOrder = order;
        var model = new LetterDetailDto
        {
            Letter = new LetterDto { LetterId = 5, LetterNumber = "LTR-5", LetterType = "Internal", Subject = "Hello", Status = "Sent", Priority = "Normal", ConfidentialityLevel = "Internal", LetterDate = new DateTime(2026, 3, 1) }
        };
        var result = new ViewResult { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(c.ViewData) { Model = model } };
        return await RazorViewRenderer.RenderAsync(c, result, "~/Views/Letter/Details.cshtml");
    }

    // ------------------------------------------------------------------ LetterController (cartable operations)

    private static Letter SentLetter(PEMSDbContext ctx, int id, int author = 101, int[]? to = null, string conf = "Internal")
    {
        var l = new Letter { LetterId = id, LetterType = "Internal", LetterNumber = "N-" + id, LetterDate = DateTime.Today, Subject = "S" + id, ConfidentialityLevel = conf, Status = "Sent", RegisteredById = author, IsActive = true };
        foreach (var e in to ?? new[] { 2 }) l.Recipients.Add(new LetterRecipient { RecipientType = "To", EmployeeId = e });
        ctx.Letters.Add(l); ctx.SaveChanges();
        return l;
    }

    // SCRUM-716: the bulk action and the empty trash are the operations Letter.Bulk and Letter.EmptyTrash
    private static (PEMS.Web.Services.LetterOperations Ops, PEMSDbContext Ctx, LetterCartableService Cart) Operations(string name, ICurrentUserService? user = null)
    {
        var ctx = LetterTestWorld.NewContext("CartOps_" + name);
        var cart = new LetterCartableService(ctx, new OrganizationSubordinateResolver(ctx));
        var letters = new LetterService(ctx, sanitizer: null!, knowledgeBaseService: null!);
        var ops = new PEMS.Web.Services.LetterOperations(ctx, letters, null!, null!, null!, user ?? new FakeUser(), new FakePermissionService { AllowAll = false }, null!, cart, new LetterTagTreeService(ctx));
        return (ops, ctx, cart);
    }

    [Fact]
    public async Task Bulk_TrashAndRestore_GoThroughTheCartableService()
    {
        var (ops, ctx, _) = Operations(nameof(Bulk_TrashAndRestore_GoThroughTheCartableService));
        SentLetter(ctx, 700);
        var trashed = await ops.BulkAsync("trash", new[] { 700 }, null, null);
        Assert.True(trashed.Success);
        Assert.True(ctx.LetterUserStates.Single().IsTrashed);
        Assert.Contains("1 letter(s) moved to trash", trashed.Error);

        await ops.BulkAsync("restore", new[] { 700 }, null, null);
        Assert.False(ctx.LetterUserStates.Single().IsTrashed);
        Assert.Equal("Sent", ctx.Letters.Single(x => x.LetterId == 700).Status);   // the letter itself never changes
    }

    [Fact]
    public async Task Bulk_TagAndUntag_UseTheChosenTag_AndNeedOne()
    {
        var (ops, ctx, _) = Operations(nameof(Bulk_TagAndUntag_UseTheChosenTag_AndNeedOne));
        SentLetter(ctx, 701);
        ctx.Tags.Add(new Tag { TagId = 1, Name = "T" }); await ctx.SaveChangesAsync();

        var none = await ops.BulkAsync("tag", new[] { 701 }, null, null);
        Assert.False(none.Success);
        Assert.Equal("No tag was chosen.", none.Error);
        Assert.Empty(ctx.LetterTags);

        await ops.BulkAsync("tag", new[] { 701 }, null, 1);
        Assert.Single(ctx.LetterTags);
        await ops.BulkAsync("untag", new[] { 701 }, null, 1);
        Assert.Empty(ctx.LetterTags);
    }

    [Fact]
    public async Task Bulk_ReadUnread_AndNothingToDoIsReportedHonestly()
    {
        var (ops, ctx, _) = Operations(nameof(Bulk_ReadUnread_AndNothingToDoIsReportedHonestly));
        SentLetter(ctx, 702);
        await ops.BulkAsync("read", new[] { 702 }, null, null);
        Assert.True(ctx.LetterRecipients.Single().IsRead);
        var again = await ops.BulkAsync("read", new[] { 702 }, null, null);
        Assert.False(again.Success);
        Assert.Equal("Nothing to change for the selected letters.", again.Error);
    }

    [Fact]
    public async Task Bulk_CannotTouchLettersTheUserCannotSee()
    {
        var (ops, ctx, _) = Operations(nameof(Bulk_CannotTouchLettersTheUserCannotSee), new FakeUser { UserId = 105 });
        SentLetter(ctx, 703, to: new[] { 2 }, conf: "Restricted");
        var result = await ops.BulkAsync("trash", new[] { 703 }, null, null);
        Assert.Empty(ctx.LetterUserStates);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task EmptyTrash_PurgesOnlyTheCurrentUsersTrash()
    {
        var (ops, ctx, cart) = Operations(nameof(EmptyTrash_PurgesOnlyTheCurrentUsersTrash));
        SentLetter(ctx, 704, to: new[] { 2, 3 });
        await cart.TrashAsync(new[] { 704 }, 102, false);
        await cart.TrashAsync(new[] { 704 }, 103, false);

        var r = await ops.EmptyTrashAsync();
        Assert.True(r.Success);
        Assert.True(ctx.LetterUserStates.Single(s => s.ApplicationUserId == 102).IsPurged);
        Assert.False(ctx.LetterUserStates.Single(s => s.ApplicationUserId == 103).IsPurged);
    }

    // ------------------------------------------------------------------ static guarantees on the front-end files
    [Fact]
    public void SettingsScript_NeverUsesInnerHtml_AndSendsTheAntiforgeryHeader()
    {
        var settings = Web("wwwroot", "js", "pems-cartable-settings.js");
        var shell = Web("wwwroot", "js", "pems-cartable.js");
        Assert.DoesNotContain("innerHTML", settings);
        Assert.DoesNotContain("innerHTML", shell);
        Assert.DoesNotContain("eval" + "(", settings + shell);
        Assert.Contains("RequestVerificationToken", shell);
        Assert.Contains("textContent", settings);
    }

    [Fact]
    public void Stylesheet_UsesThemeTokens_AndOverridesThemesWithRootDataTheme()
    {
        var css = Web("wwwroot", "css", "pems-cartable.css");
        Assert.Contains(":root[data-theme=\"dark\"]", css);
        Assert.Contains(":root[data-theme=\"turquoise\"]", css);
        Assert.Contains("var(--pems-surface)", css);
        Assert.DoesNotContain("backdrop-filter", css);                    // would trap dropdowns inside their parent (see the theme cascade note)
        Assert.DoesNotContain(".ct-count.alert", css);                    // Bootstrap's .alert would restyle it
        Assert.Contains("prefers-reduced-motion", css);
        Assert.Contains("@media (max-width: 991.98px)", css);
    }

}
