using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.ViewModels;

namespace PEMS.Tests.Rcm;

/// <summary>
/// HTML واقعی صفحه‌های RCM (View کامپایل‌شده‌ی PEMS.Web + داده‌ی InMemory): برچسب‌ها از Resource ترجمه می‌شوند (نه کد خام)،
/// هویت تجهیز فقط‌خواندنی است، فرم Plan هرگز equipmentId نمی‌پذیرد، و هیچ View در زمان اجرا خطا نمی‌دهد.
/// </summary>
public class RcmViewRenderTests
{
    public RcmViewRenderTests() => CultureInfo.CurrentUICulture = new CultureInfo("en");

    private static async Task<string> Render(Controller c, IActionResult result, string view)
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en"); // فرهنگ جریان async همین تست (کلاس‌های موازی فرهنگ را عوض می‌کنند)
        // Razor کاراکترهای غیر ASCII (مثل «–») را Encode می‌کند؛ مقایسه روی متن Decode‌شده انجام می‌شود
        return System.Net.WebUtility.HtmlDecode(await RazorViewRenderer.RenderAsync(c, (ViewResult)result, view));
    }

    private sealed record Fixture(RcmEnv Env, int StrategyId, int ClassId, int ModeId, int TagId, int AssignmentId);

    private static async Task<Fixture> BuildAsync(string name)
    {
        var env = await RcmTestFactory.CreateEnvAsync("View_" + name);
        var (classId, modeId) = await RcmTestFactory.SeedCatalogAsync(env.Ctx);
        var strategyId = await RcmTestFactory.CreateActiveStrategyAsync(env, classId, modeId);
        var tag = await RcmTestFactory.AddEquipmentAsync(env.Ctx, "80-FT-6001", classId);
        await RcmTestFactory.AddCriticalityAsync(env.Ctx, tag.EquipmentId, "A");
        await env.Assignments.AssignAsync(strategyId, new[] { tag.EquipmentId }, RcmTestFactory.Actor(5));
        var assignmentId = env.Ctx.MaintenanceStrategyAssignments.Single().MaintenanceStrategyAssignmentId;
        return new Fixture(env, strategyId, classId, modeId, tag.EquipmentId, assignmentId);
    }

    [Fact]
    public async Task StrategyIndex_ShowsLocalizedLabels_NotRawCodes()
    {
        var f = await BuildAsync(nameof(StrategyIndex_ShowsLocalizedLabels_NotRawCodes));
        var c = f.Env.StrategyController();

        var html = await Render(c, await c.Index(null), "/Views/MaintenanceStrategy/Index.cshtml");

        Assert.Contains("Leakage PUMP_T", html);                       // نام حالت خرابی کاتالوگ
        Assert.Contains("Preventive (scheduled restoration/discard)", html); // برچسب ترجمه‌شده‌ی نوع، نه "Preventive"
        Assert.Contains("Operational", html);
        Assert.Contains("Active", html);
        Assert.DoesNotContain(">RunToFailure<", html);                 // کد خام هرگز به‌عنوان متن نمایش داده نمی‌شود
        Assert.Contains("Test PUMP_T", html);                          // کلاس تجهیز
    }

    [Fact]
    public async Task StrategyDetails_ShowsTagIdentityVersionsHistoryAndAssignmentActions()
    {
        var f = await BuildAsync(nameof(StrategyDetails_ShowsTagIdentityVersionsHistoryAndAssignmentActions));
        var c = f.Env.StrategyController();

        var html = await Render(c, await c.Details(f.StrategyId), "/Views/MaintenanceStrategy/Details.cshtml");

        Assert.Contains("80-FT-6001", html);
        Assert.Contains("A – critical", html);
        Assert.Contains("Change history", html);
        Assert.Contains("Activated", html);
        Assert.Contains("name=\"overrideJustification\"", html);
    }

    [Fact]
    public async Task StrategyForm_HasMandatoryRcmFields_AndNoTagFreeTextField()
    {
        var f = await BuildAsync(nameof(StrategyForm_HasMandatoryRcmFields_AndNoTagFreeTextField));
        var c = f.Env.StrategyController();

        var html = await Render(c, await c.Create(equipmentId: f.TagId), "/Views/MaintenanceStrategy/Edit.cshtml");

        Assert.Contains("name=\"FailureModeId\"", html);
        Assert.Contains("name=\"Justification\"", html);
        Assert.Contains("name=\"ConsequenceCategory\"", html);
        Assert.Contains("data-equipment-identity", html);              // هویت تجهیز فقط‌خواندنی (تگ/نام/کلاس/بحرانی‌بودن)
        Assert.Contains("80-FT-6001", html);
        Assert.DoesNotContain("name=\"equipmentId\"", html);           // تگ هرگز فیلد متنی/ورودی نیست
        Assert.DoesNotContain("<option>Leakage</option>", html);       // دیگر فهرست ثابت ۷تایی نیست
    }

    [Fact]
    public async Task StrategyForm_AfterRejectedSave_ShowsTheLocalizedBusinessErrors()
    {
        var f = await BuildAsync(nameof(StrategyForm_AfterRejectedSave_ShowsTheLocalizedBusinessErrors));
        var c = f.Env.StrategyController();
        var bad = RcmTestFactory.ValidInput(f.ClassId, f.ModeId, "RunToFailure", "Safety");
        bad.Justification = "";

        var result = await c.Create(bad);
        var html = await Render(c, result, "/Views/MaintenanceStrategy/Edit.cshtml");

        Assert.Contains("alert-danger", html);
        Assert.Contains("name=\"Justification\"", html);
    }

    [Fact]
    public async Task BulkAssign_ShowsCriticalityAndConflicts_ForTheSelectedStrategy()
    {
        var f = await BuildAsync(nameof(BulkAssign_ShowsCriticalityAndConflicts_ForTheSelectedStrategy));
        var second = await RcmTestFactory.CreateActiveStrategyAsync(f.Env, f.ClassId, f.ModeId, "Predictive");
        var c = f.Env.StrategyController();

        var html = await Render(c, await c.BulkCreate(null, null, null, "80-FT", strategyId: second, classId: f.ClassId),
            "/Views/MaintenanceStrategy/BulkCreate.cshtml");

        Assert.Contains("A – critical", html);
        Assert.Contains("Conflicting strategy for the same failure mode", html);
        Assert.Contains("name=\"strategyId\"", html);
    }

    [Fact]
    public async Task PlanForm_PrefillsTagAsReadOnlyIdentity_AndTheAssignmentIsTheOnlyKey()
    {
        var f = await BuildAsync(nameof(PlanForm_PrefillsTagAsReadOnlyIdentity_AndTheAssignmentIsTheOnlyKey));
        await RcmTestFactory.AddJobPlanAsync(f.Env.Ctx);
        var operations = f.Env.PlanOperations();

        var row = Assert.Single(await operations.FormOfAsync(f.AssignmentId));

        Assert.Equal("80-FT-6001", row.Tag);
        Assert.Equal(f.AssignmentId, row.Id);                                              // the form is /f/maintenance-plan-create/{assignmentId}: no equipment id or strategy id is read from the client
        Assert.Equal("80-FT-6001 - Leakage PUMP_T - Preventive", row.Title);              // the suggested title
        Assert.StartsWith("15", row.SuggestedInterval);                                    // criticality A factor
        var form = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(PEMS.Tests.TestSupport.SupportedReleases.RepoRoot(), "PEMS.Persistence", "Definitions", "System", "form", "core", "maintenance-plan-create.json")))!["payload"]!;
        var names = form["fields"]!.AsArray().Select(x => (string)x!["name"]!).ToList();
        Assert.DoesNotContain("equipmentId", names);
        Assert.DoesNotContain("maintenanceStrategyId", names);
    }

    [Fact]
    public async Task PlanForm_WithoutAssignment_ListsDistinctChoices()
    {
        var f = await BuildAsync(nameof(PlanForm_WithoutAssignment_ListsDistinctChoices));

        var choices = await f.Env.PlanOperations().AssignmentChoicesAsync(f.TagId, null);

        var choice = Assert.Single(choices);
        Assert.Equal("80-FT-6001", choice.Tag);
        Assert.Equal(1, choice.StrategyVersion);
    }

    [Fact]
    public async Task PlanForm_UnknownAssignment_GivesNoRow_NotAnException()
    {
        var env = await RcmTestFactory.CreateEnvAsync("View_PlanUnknown");

        Assert.Empty(await env.PlanOperations().FormOfAsync(424242));                       // the form host answers 404 for an id that gives no row
    }

    [Fact]
    public async Task TagCoverageAndCatalogPages_RenderWithoutErrors()
    {
        var f = await BuildAsync(nameof(TagCoverageAndCatalogPages_RenderWithoutErrors));
        var c = f.Env.StrategyController();

        var tag = await Render(c, await c.Tag(f.TagId), "/Views/MaintenanceStrategy/Tag.cshtml");
        var coverage = await Render(c, await c.Coverage(), "/Views/MaintenanceStrategy/Coverage.cshtml");

        Assert.Contains("Maintenance of tag", tag);
        Assert.Contains("Strategy coverage", coverage);
        Assert.Contains("Coverage by criticality", coverage);

        var catalog = new FailureModeCatalogController(f.Env.Ctx, new PEMS.Persistence.Services.FailureModeCatalogService(f.Env.Ctx),
            f.Env.User, f.Env.Permissions).WithTempData();
        var index = await Render(catalog, await catalog.Index(null, null), "/Views/FailureModeCatalog/Index.cshtml");
        var edit = await Render(catalog, await catalog.Edit((int?)null), "/Views/FailureModeCatalog/Edit.cshtml");
        Assert.Contains("Failure mode catalogue", index);
        Assert.Contains("name=\"Code\"", edit);
    }

    [Fact]
    public async Task ImportPage_RendersUploadFormsAndResults()
    {
        var env = await RcmTestFactory.CreateEnvAsync("View_Import");
        var controller = new StrategyImportController(env.Ctx, new PEMS.Persistence.Services.StrategyImportService(env.Ctx, env.Commands, env.Assignments),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), env.User, env.Permissions).WithTempData();

        var html = await Render(controller, await controller.Index(), "/Views/StrategyImport/Index.cshtml");

        Assert.Contains("name=\"file\"", html);
        Assert.Contains("Download template", html);
        _ = typeof(StrategyImportViewModel);
    }
}
