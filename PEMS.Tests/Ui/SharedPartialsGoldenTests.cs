using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Models;
using PEMS.Web.ViewModels;
using System.Globalization;

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
        Assert.Equal(Lf(File.ReadAllText(path)), Lf(actual));   // line-ending style only: Windows checkouts convert the files (autocrlf)
    }

    private static string Lf(string s) => s.Replace("\r\n", "\n");

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

    // ---- _Pagination (SCRUM-418): the markup the 54 list pages receive must not change when the partial moves into PEMS.UI
    private static async Task<string> InCultureAsync(string culture, Func<Task<string>> render)
    {
        var (c, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try { return await render(); }
        finally { CultureInfo.CurrentCulture = c; CultureInfo.CurrentUICulture = ui; }
    }

    private static PaginationViewModel Pager(int page, int pages, int total, string? gridKey = null, int pageSize = 25, string query = "") => new()
    {
        Page = page, TotalPages = pages, TotalCount = total, BaseUrl = "/WorkOrders", ExistingQueryString = query, GridKey = gridKey, PageSize = pageSize
    };

    [Theory]
    [InlineData("en", 1, 5, 120, null, "q=pump&status=open", "pagination-en-first")]
    [InlineData("en", 3, 10, 250, "wo", "", "pagination-en-middle-gridkey")]
    [InlineData("en", 10, 10, 250, "wo", "q=a", "pagination-en-last-gridkey")]
    [InlineData("en", 0, 0, 0, null, "", "pagination-en-empty")]
    [InlineData("en", 1, 1, 7, "wo", "", "pagination-en-single-gridkey")]
    [InlineData("fa", 3, 10, 250, "wo", "", "pagination-fa-middle-gridkey")]
    [InlineData("fa", 1, 5, 120, null, "q=pump", "pagination-fa-first")]
    [InlineData("fa", 0, 0, 0, "wo", "", "pagination-fa-empty-gridkey")]
    public async Task Pagination_RendersTheSameMarkup(string culture, int page, int pages, int total, string? gridKey, string query, string golden) =>
        AssertGolden(golden, await InCultureAsync(culture, () => RenderAsync("~/Views/Shared/_Pagination.cshtml", Pager(page, pages, total, gridKey, 25, query))));
}
