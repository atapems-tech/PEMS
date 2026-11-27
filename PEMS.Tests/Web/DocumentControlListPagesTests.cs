using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 wave 1 (document control): DocumentRegister, DocumentRevisions, Transmittal, TechnicalQuery, MDB, MOC and
/// DocumentWorkflow are built from the PEMS.UI list-page component. Each page keeps its own filters, columns and actions;
/// the old hand-written frame (content cards, KPI cards, inline column-resize script) is gone. DCC is a desk, not a list.
/// </summary>
public class DocumentControlListPagesTests
{
    private static Task<string> Render<TController>(string view, object model, Action<ViewDataDictionary> data) where TController : Controller
    {
        // rendering only needs the controller's ViewData; its constructor (services) is not used
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));
        data(controller.ViewData);
        var result = new ViewResult { ViewData = new ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, view);
    }

    private static PagedResult<T> Page<T>(params T[] items) => new() { Items = items.ToList(), Page = 1, PageSize = 25, TotalCount = items.Length };

    // the controllers pass anonymous objects read through dynamic; ExpandoObject behaves the same across assemblies
    private static dynamic Obj(params (string Key, object? Value)[] values)
    {
        IDictionary<string, object?> o = new ExpandoObject();
        foreach (var (k, v) in values) o[k] = v;
        return o;
    }

    private static List<dynamic> Projects() => new() { Obj(("ProjectId", 1), ("ProjectName", "JPC")), Obj(("ProjectId", 2), ("ProjectName", "BIDBOLAND")) };

    private static void AssertBuiltFromTheComponent(string html, string gridKey, int rows)
    {
        Assert.Contains("class=\"lp-page\"", html);
        Assert.Contains("class=\"lp-toolbar\"", html);
        Assert.Matches($"<table class=\"[^\"]*lp-table[^\"]*\" data-grid-key=\"{gridKey}\"", html);
        Assert.Equal(rows, Regex.Matches(html, "<tr class=\"lp-row\"").Count);
        Assert.Contains($"data-lp-reset-columns=\"{gridKey}\"", html);
        Assert.Contains("class=\"lp-footer\"", html);
        // the old hand-written frame is gone
        Assert.DoesNotContain("content-card", html);
        Assert.DoesNotContain("PemsDataGrid.initColumnResize", html);
        Assert.DoesNotContain("class=\"badge ", html);
    }


    private static EngineeringDocumentDto Doc(int id) => new()
    {
        DocumentId = id, DocumentNo = "JPC-ME-DS-00" + id, Title = "Pump datasheet " + id, DocumentType = "DS", ProjectName = "JPC",
        DisciplineName = "Mechanical", RevisionNo = "B", Status = "Approved", CreatedDate = new DateTime(2026, 10, 1), IsFrozen = id == 2
    };

    // ---------- DocumentRevisions ----------

    // ---------- TechnicalQuery ----------

    // ---------- MDB ----------

    // ---------- DocumentWorkflow ----------

}
