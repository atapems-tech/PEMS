using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Models;

namespace PEMS.Tests.Ui;

/// <summary>
/// Golden output of shared partials that live in PEMS.UI but are rendered by PEMS.Web (SCRUM-29): moving a partial between projects
/// must not change one byte of what pages receive. Expected HTML is in <c>Ui/Golden/*.html</c>. To record a new golden file set
/// <c>PEMS_GOLDEN_RECORD=1</c> and run the test once on the code that is known to be right, then review the file in the diff.
/// </summary>
public class SharedPartialsGoldenTests
{
    private static async Task<string> RenderAsync(string view, object? model)
    {
        var host = new PermissionMatrixController(null!, null!, null!);
        host.ViewData.Model = model;
        var result = new ViewResult { ViewData = new ViewDataDictionary(host.ViewData) { Model = model } };
        return await RazorViewRenderer.RenderAsync(host, result, view);
    }

    private static void AssertGolden(string name, string actual)
    {
        var path = Path.Combine(FindGoldenDirectory(), name + ".html");
        if (Environment.GetEnvironmentVariable("PEMS_GOLDEN_RECORD") == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }
        Assert.True(File.Exists(path), $"Golden file missing: {path} (record it with PEMS_GOLDEN_RECORD=1)");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static string FindGoldenDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "PEMS.Tests", "Ui", "Golden"))) dir = dir.Parent;
        return dir is null ? throw new DirectoryNotFoundException("PEMS.Tests/Ui/Golden") : Path.Combine(dir.FullName, "PEMS.Tests", "Ui", "Golden");
    }

    [Fact]
    public async Task KpiCard_WithLink_RendersTheSameMarkup() =>
        AssertGolden("kpi-card-link", await RenderAsync("~/Views/Shared/_KpiCard.cshtml", new KpiCardViewModel
        {
            Title = "Open work orders", Count = 1234, IconClass = "bi-tools", ColorClass = "bg-icon-green",
            Url = "/WorkOrders?status=open", Hint = "Work orders not yet closed"
        }));

    [Fact]
    public async Task KpiCard_WithoutLinkAndZeroCount_RendersTheSameMarkup() =>
        AssertGolden("kpi-card-plain-zero", await RenderAsync("~/Views/Shared/_KpiCard.cshtml", new KpiCardViewModel
        {
            Title = "Held runs", Count = 0
        }));

    [Fact]
    public async Task LoadingOverlay_RendersTheSameMarkup() =>
        AssertGolden("loading-overlay", await RenderAsync("~/Views/Shared/_PemsLoadingOverlay.cshtml", null));
}
