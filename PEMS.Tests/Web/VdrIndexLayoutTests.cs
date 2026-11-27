using System.Dynamic;
using System.Reflection;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Helpers;
using PEMS.Web.Security;

namespace PEMS.Tests.Web;

/// <summary>
/// فهرست VDR با همان قاب فهرست مدارک مهندسی (SCRUM-409، پیرو SCRUM-408): صفحه‌ی تمام‌قد، یک نوار ابزار آیکنی، فیلترها در منوی کشویی
/// با نشان تعداد و چیپ، شمارش‌ها و ریسک فروشنده پیش‌فرض پنهان، جستجوی متنی و خروجی Excel، ستون‌های پروژه/دیسیپلین.
/// View واقعی رندر می‌شود (Localizer متن انگلیسی resx را می‌دهد).
/// </summary>
public class VdrIndexLayoutTests
{
    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1;
        public string? UserName => "u";
        public string? FullName => "U";
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles => new[] { "Staff" };
    }

    private static VDRListItemDto Row(string number = "PKG-EL-0231-01", string? discipline = "Electrical") => new()
    {
        VDRItemId = 1, DocumentNumber = number, DocumentDescription = "Panel datasheet", DocumentTypeCode = "DS", VendorName = "KASRAVAND",
        ProjectName = "Polymer Plant Development", DisciplineName = discipline, WorkflowStatus = "Submitted"
    };

    private static async Task<string> RenderAsync(IEnumerable<VDRListItemDto> rows, int? projectId = null, int? vendorId = null, string? status = null,
        string? search = null, VDRDashboardDto? dash = null)
    {
        var (_, letters, ctx, _) = LetterTestWorld.Services("vdrindex");
        var c = new LetterController(letters, null!, ctx, new FakeUser(), new FakePermissionService(), null!, null!, null!, null!, null!, null!).WithTempData();
        c.ViewBag.Dashboard = dash ?? new VDRDashboardDto { TotalItems = 7, OverdueItems = 3, SlaOverdueItems = 2, OnTimePercentage = 80 };
        // anonymous types do not cross assemblies for the view's dynamic binder, so the lookups are expandos
        dynamic project = new ExpandoObject(); project.ProjectId = 1; project.ProjectName = "Polymer Plant Development";
        dynamic vendor = new ExpandoObject(); vendor.VendorId = 9; vendor.Name = "KASRAVAND";
        c.ViewBag.Projects = new List<dynamic> { project };
        c.ViewBag.Vendors = new List<dynamic> { vendor };
        c.ViewBag.SelectedProjectId = projectId;
        c.ViewBag.SelectedVendorId = vendorId;
        c.ViewBag.SelectedStatus = status;
        c.ViewBag.Search = search;

        var list = rows.ToList();
        var model = new PagedResult<VDRListItemDto> { Items = list, Page = 1, PageSize = 25, TotalCount = list.Count };
        var result = new ViewResult { ViewData = new ViewDataDictionary(c.ViewData) { Model = model } };
        return await RazorViewRenderer.RenderAsync(c, result, "~/Views/VDR/Index.cshtml");
    }

    // ------------------------------------------------------------------ query, search and export

    private static async Task<PEMS.Persistence.Context.PEMSDbContext> SeedAsync(string name)
    {
        var ctx = LetterTestWorld.NewContext(name);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "P1", ProjectName = "Polymer Plant Development", IsActive = true });
        ctx.Vendors.Add(new Vendor { VendorId = 9, Name = "KASRAVAND", IsActive = true });
        ctx.Vendors.Add(new Vendor { VendorId = 10, Name = "PUMPCO", IsActive = true });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 4, Name = "Electrical", IsActive = true });
        ctx.VDRItems.Add(new VDRItem
        {
            DocumentId = 1, ProjectId = 1, VendorId = 9, DisciplineId = 4, DocumentNumber = "A-1", DocumentDescription = "Switchgear datasheet",
            DocumentTypeCode = "DS", WorkflowStatus = "Planned", IsActive = true
        });
        ctx.VDRItems.Add(new VDRItem
        {
            DocumentId = 2, ProjectId = 1, VendorId = 9, DisciplineId = null, DisciplineCodeRaw = "INST", DocumentNumber = "A-2",
            DocumentDescription = "Transmitter hook-up", DocumentTypeCode = "DW", WorkflowStatus = "Planned", IsActive = true
        });
        ctx.VDRItems.Add(new VDRItem
        {
            DocumentId = 3, ProjectId = 1, VendorId = 10, DocumentNumber = "B-7", DocumentDescription = "Pump curve",
            DocumentTypeCode = "DS", WorkflowStatus = "Closed", IsActive = true
        });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task ListQuery_ProjectsProjectName_AndDisciplineNameWithRawCodeFallback()
    {
        var ctx = await SeedAsync("vdrquery");

        var page = await new VDRService(ctx).GetPagedAsync(null, null, null, 1, 25);

        var byNumber = page.Items.ToDictionary(i => i.DocumentNumber);
        Assert.Equal("Polymer Plant Development", byNumber["A-1"].ProjectName);
        Assert.Equal("Electrical", byNumber["A-1"].DisciplineName);
        Assert.Equal("INST", byNumber["A-2"].DisciplineName);       // imported item without a discipline row shows its raw code
    }

    [Theory]
    [InlineData("A-1", new[] { "A-1" })]                 // document number
    [InlineData("datasheet", new[] { "A-1" })]           // description
    [InlineData("PUMPCO", new[] { "B-7" })]              // vendor name
    [InlineData("  ", new[] { "A-1", "A-2", "B-7" })]    // blank = no search
    [InlineData("nothing-like-this", new string[0])]
    public async Task Search_MatchesNumberDescriptionOrVendor(string term, string[] expected)
    {
        var ctx = await SeedAsync("vdrsearch_" + Guid.NewGuid().ToString("N"));

        var page = await new VDRService(ctx).GetPagedAsync(null, null, null, 1, 25, term);

        Assert.Equal(expected.OrderBy(x => x), page.Items.Select(i => i.DocumentNumber).OrderBy(x => x));
        Assert.Equal(expected.Length, page.TotalCount);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found; set PEMS_REPO_ROOT");
    }
}
