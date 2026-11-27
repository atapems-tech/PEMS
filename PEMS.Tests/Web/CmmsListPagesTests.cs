using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.ViewModels;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 wave 2 (CMMS): Work Orders, Work Requests, Equipment, Functional Locations, Instruments, Letter Tags,
/// Maintenance Plans, Maintenance Strategies, Job Plans, Spare Parts, Permits, Inspections, Findings and Assets are built from
/// the PEMS.UI list-page component. Each page keeps its own filters, columns, modals, actions and permissions; the old
/// hand-written frame (content cards, count cards, status button rows, inline column-resize script) is gone.
/// Wave 2b adds Criticality Assessments, Equipment Criticality, Mechanical Equipment, the Failure Mode Catalogue, Shift
/// Handovers, RCM / FMEA coverage and the Equipment / Functional Location assignment batches.
/// </summary>
public class CmmsListPagesTests
{
    private static readonly string[] Pages =
    {
        // wave 2b
    };

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

    /// <summary>The shared frame. <paramref name="gridKey"/> null = a short list without stored column widths or paging.</summary>
    private static void AssertBuiltFromTheComponent(string html, string? gridKey, int rows, bool keepsDataBadges = false)
    {
        Assert.Contains("class=\"lp-page\"", html);
        Assert.Contains("class=\"lp-toolbar\"", html);
        Assert.Equal(rows, Regex.Matches(html, "<tr class=\"lp-row[ \"]").Count);
        if (gridKey != null)
        {
            Assert.Matches($"<table class=\"[^\"]*lp-table[^\"]*\" data-grid-key=\"{gridKey}\"", html);
            Assert.Contains($"data-lp-reset-columns=\"{gridKey}\"", html);
            Assert.Contains("class=\"lp-footer\"", html);
        }
        // the old hand-written frame is gone
        Assert.DoesNotContain("content-card", html);
        Assert.DoesNotContain("PemsDataGrid.initColumnResize", html);
        Assert.DoesNotContain("btn-group btn-group-sm flex-wrap", html);       // the old status button row (paging keeps its own group)
        // status badges became tone pills; a badge stays only where its colour is data (tag colour, RCM criticality helpers)
        if (!keepsDataBadges) Assert.DoesNotContain("class=\"badge ", html);
    }

    private static string Chips(string html) => Regex.Match(html, "<div class=\"lp-chips\">(.*?)</div>", RegexOptions.Singleline).Value;
    private static string Kpis(string html, string id) => Regex.Match(html, $"id=\"{id}\" hidden(?:=\"hidden\")?>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value;

    // ---------- Work orders ----------




    // ---------- Work requests ----------

    // ---------- Letter tags: the page is a definition since SCRUM-754 (tags, tag-create, tag-delete) ----------

    // ---------- Inspections ----------

    // ---------- Findings ----------

    // ---------- Maintenance plans ----------

    // ---------- Job plans ----------

    // ---------- Permits ----------

    // ---------- Functional locations ----------

    // ---------- Instruments ----------

    // ---------- Maintenance strategies ----------



    // ---------- Spare parts ----------

    // ---------- Assets ----------

    // ================= wave 2b: the remaining CMMS lists =================

    // ---------- Mechanical equipment ----------

    // ---------- Shift handovers ----------

    // ---------- Production orders and the procurement cartable (SCRUM-337) ----------

    // ---------- Equipment <-> functional location assignment ----------

}
