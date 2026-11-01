using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.Application.DTOs;
using PEMS.Tests.TestSupport;
using PEMS.UI.TagHelpers;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 RelatedList: the child collection of a detail page (section with title, count and empty text, child table,
/// add form, delete form per row, in-place row editing) and the first pages built from it (JobPlan details and the three
/// Administration editors).
/// </summary>
public class RelatedListComponentTests
{
    private static async Task<string> Run(TagHelper helper, string tag, string childHtml = "")
    {
        var items = new Dictionary<object, object>();
        var context = new TagHelperContext(tag, new TagHelperAttributeList(), items, "u1");
        var output = new TagHelperOutput(tag, new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childHtml)));
        helper.Init(context);
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Section_HasATitledHeadingWithCount_AndTheEmptyTextOnlyWhenEmpty()
    {
        var html = await Run(new PemsRelatedListTagHelper { Id = "tasks", Icon = "bi-list-ol", Title = "Tasks", Count = 3, EmptyText = "None yet" }, "pems-related-list", "<p>rows</p>");
        Assert.StartsWith("<section", html);
        Assert.Contains("aria-labelledby=\"tasksTitle\"", html);
        Assert.Contains("<h3 id=\"tasksTitle\">", html);
        Assert.Contains("<span class=\"lp-total\">3</span>", html);
        Assert.Contains("<p>rows</p>", html);
        Assert.DoesNotContain("None yet", html);

        var empty = await Run(new PemsRelatedListTagHelper { Title = "Tasks", Empty = true, EmptyText = "None yet" }, "pems-related-list");
        Assert.Contains("<p class=\"lp-rel-empty\">None yet</p>", empty);
    }

    [Fact]
    public async Task AddForm_PostsTheParentKey_AndTheFields()
    {
        var html = await Run(new PemsRelatedAddTagHelper { Action = "/JobPlan/AddTask", ParentName = "jobPlanId", ParentValue = "7", SubmitLabel = "Add" },
            "pems-related-add", "<div><input name=\"title\" /></div>");
        Assert.Matches("<form action=\"/JobPlan/AddTask\" class=\"lp-rel-add\" method=\"post\">", html);
        Assert.Contains("<input name=\"jobPlanId\" type=\"hidden\" value=\"7\" />", html);
        Assert.Contains("<input name=\"title\" />", html);
        Assert.Matches("<button class=\"[^\"]*lp-rel-submit\" type=\"submit\">", html);
    }

    [Fact]
    public async Task Remove_PostsTheRowAndParentKeys_WithALabelledButton_AndAsksOnlyWhenTold()
    {
        var html = await Run(new PemsRelatedRemoveTagHelper { Action = "/JobPlan/RemoveTask", KeyName = "maintenanceTaskId", KeyValue = "5", ParentName = "jobPlanId", ParentValue = "7", Label = "Remove" },
            "pems-related-remove");
        Assert.Contains("name=\"maintenanceTaskId\" type=\"hidden\" value=\"5\"", html);
        Assert.Contains("name=\"jobPlanId\" type=\"hidden\" value=\"7\"", html);
        Assert.Contains("title=\"Remove\"", html);
        Assert.Contains("<span class=\"visually-hidden\">Remove</span>", html);
        Assert.DoesNotContain("confirm(", html);

        var asked = await Run(new PemsRelatedRemoveTagHelper { Action = "/x", KeyName = "id", KeyValue = "1", Label = "Remove", Confirm = "Sure?" }, "pems-related-remove");
        Assert.Contains("data-confirm=\"Sure?\"", asked);
        Assert.Contains("onsubmit=\"return confirm(this.dataset.confirm)\"", asked);
    }

    [Fact]
    public async Task RowForm_IsHiddenAndNamed_AndSaveSubmitsIt()
    {
        var form = await Run(new PemsRelatedRowFormTagHelper { Id = "num_PO", Action = "/Admin/Numbering/Save" }, "pems-related-row-form", "<input type=\"hidden\" name=\"DocumentType\" value=\"PO\" />");
        Assert.Matches("<form action=\"/Admin/Numbering/Save\" hidden=\"hidden\" id=\"num_PO\" method=\"post\">", form);
        Assert.Contains("name=\"DocumentType\"", form);

        var save = await Run(new PemsRelatedSaveTagHelper { FormId = "num_PO", Label = "Save" }, "pems-related-save");
        Assert.Contains("form=\"num_PO\"", save);
        Assert.Contains("type=\"submit\"", save);
        Assert.Contains("title=\"Save\"", save);
    }

    // ---------- pages ----------

    private static Task<string> Render<TController>(string view, object model, Action<ViewDataDictionary> data) where TController : Controller
    {
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));
        data(controller.ViewData);
        var result = new ViewResult { ViewData = new ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, view);
    }

    private static dynamic Obj(params (string Key, object? Value)[] values)
    {
        IDictionary<string, object?> o = new ExpandoObject();
        foreach (var (k, v) in values) o[k] = v;
        return o;
    }

    [Fact]
    public async Task JobPlan_TasksAndMaterials_AreRelatedLists_WithAddAndRemove()
    {
        var plan = new JobPlanDetailDto
        {
            JobPlanId = 4, Title = "PM pump",
            Tasks = new() { new MaintenanceTaskDto { MaintenanceTaskId = 11, TaskNumber = 10, Title = "Isolate" } },
            RequiredMaterials = new(),
        };
        var html = await Render<JobPlanController>("/Views/JobPlan/Details.cshtml", plan, v =>
        {
            v["AllSkills"] = new List<dynamic>();
            v["MaterialItems"] = new List<dynamic> { Obj(("MaterialItemId", 3), ("MaterialCode", "M-3"), ("Description", "Bearing")) };
            v["EquipmentClasses"] = new List<dynamic>();
        });

        Assert.Contains("id=\"jpTasks\"", html);
        Assert.Contains("id=\"jpMaterials\"", html);
        Assert.Contains("name=\"maintenanceTaskId\" type=\"hidden\" value=\"11\"", html);
        Assert.Equal(2, Regex.Matches(html, "class=\"lp-rel-add\"").Count);
        Assert.Contains("<p class=\"lp-rel-empty\">", html);                               // no materials yet
        Assert.Contains("value=\"3\">M-3 - Bearing</option>", html);
    }

    [Fact]
    public async Task RoutingRules_EditEachRowInPlace()
    {
        var html = await Render<RoutingRulesController>("/Areas/Admin/Views/RoutingRules/Index.cshtml",
            new List<RoutingRuleDto> { new() { RoutingRuleId = 2, DocumentCategory = "Drawing", TargetModule = "VDR", AllowAutoRouting = true } },
            v => { v["Confidence"] = new AIConfidenceSettingsDto { HighThreshold = .9, MediumThreshold = .6 }; v["Modules"] = new[] { "VDR", "Docs" }; });

        Assert.Contains("class=\"lp-page\"", html);
        Assert.Matches("<form [^>]*hidden=\"hidden\" id=\"rule_2\"", html);
        Assert.Equal(4, Regex.Matches(html, "form=\"rule_2\"").Count);                     // module, auto route, description and the save button
        Assert.Contains("id=\"rrConfidence\"", html);
        Assert.DoesNotContain("admin-hero", html);
    }
}
