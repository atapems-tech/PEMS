using ClosedXML.Excel;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Repositories;
using PEMS.Tests.TestSupport;
using PEMS.Web.Helpers;

namespace PEMS.Tests.Engineering;

/// <summary>
/// Export فهرست مدارک مهندسی (Index): همان فیلتر و همان محدودیت دیسیپلین Index، بدون صفحه‌بندی، با سقف؛
/// و اصلاح ستون خالیِ «Issue Purpose» در Export رجیستر (Projection آن را حمل نمی‌کرد).
/// </summary>
public class EngineeringDocumentListExportTests
{
    private static EngineeringDocumentRepository CreateRepo(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory(dbName);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectCode = "TP", ProjectName = "Test Project" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 5, Code = "MECH", Name = "Mechanical" });
        ctx.Disciplines.Add(new Discipline { DisciplineId = 6, Code = "ELEC", Name = "Electrical" });
        ctx.SaveChanges();
        return new EngineeringDocumentRepository(ctx);
    }

    private static EngineeringDocument Doc(string no, int? disciplineId, string status, string title = "Doc", string? issuePurpose = null) => new()
    {
        DocumentNo = no, Title = title, DocumentType = "Datasheet", ProjectId = 1, DisciplineId = disciplineId,
        Status = status, IsActive = true, CreatedDate = DateTime.Now, IssuePurpose = issuePurpose
    };

    [Fact]
    public async Task GetAllMatching_AppliesTheSameFiltersAsTheIndex_AndIgnoresPaging()
    {
        var repo = CreateRepo(nameof(GetAllMatching_AppliesTheSameFiltersAsTheIndex_AndIgnoresPaging), out var ctx);
        for (var i = 0; i < 60; i++) ctx.EngineeringDocuments.Add(Doc($"MECH-{i:000}", 5, "Approved", "Pump datasheet"));
        ctx.EngineeringDocuments.Add(Doc("ELEC-001", 6, "Approved", "Cable schedule"));
        ctx.EngineeringDocuments.Add(Doc("MECH-DRAFT", 5, "Draft", "Pump datasheet"));
        await ctx.SaveChangesAsync();

        var filter = new EngineeringDocumentListFilter { Status = "Approved", DisciplineId = 5, PageSize = 20, Page = 3 };
        var (items, total) = await repo.GetAllMatchingAsync(filter, restrictToDiscipline: false, disciplineId: null, maxRows: 20000);

        Assert.Equal(60, total);                                   // every match, not one page of 20
        Assert.Equal(60, items.Count);
        Assert.All(items, x => { Assert.Equal("Approved", x.Status); Assert.Equal(5, x.DisciplineId); });
    }

    [Fact]
    public async Task GetAllMatching_HonoursTheUsersDisciplineScope_NeverExportsMoreThanTheListShows()
    {
        var repo = CreateRepo(nameof(GetAllMatching_HonoursTheUsersDisciplineScope_NeverExportsMoreThanTheListShows), out var ctx);
        ctx.EngineeringDocuments.Add(Doc("MECH-1", 5, "Draft"));
        ctx.EngineeringDocuments.Add(Doc("ELEC-1", 6, "Draft"));
        ctx.EngineeringDocuments.Add(Doc("NONE-1", null, "Draft"));
        await ctx.SaveChangesAsync();

        var filter = new EngineeringDocumentListFilter();
        var paged = await repo.GetPagedAsync(filter, restrictToDiscipline: true, disciplineId: 5);
        var (exported, total) = await repo.GetAllMatchingAsync(filter, restrictToDiscipline: true, disciplineId: 5, maxRows: 20000);

        Assert.Equal(paged.TotalCount, total);
        Assert.Equal(paged.Items.Select(x => x.DocumentNo).OrderBy(x => x), exported.Select(x => x.DocumentNo).OrderBy(x => x));
        Assert.DoesNotContain(exported, x => x.DocumentNo == "ELEC-1");
    }

    [Fact]
    public async Task GetAllMatching_ReturnsNoRowsButTheRealTotal_WhenAboveTheCap()
    {
        var repo = CreateRepo(nameof(GetAllMatching_ReturnsNoRowsButTheRealTotal_WhenAboveTheCap), out var ctx);
        for (var i = 0; i < 12; i++) ctx.EngineeringDocuments.Add(Doc($"D-{i}", 5, "Draft"));
        await ctx.SaveChangesAsync();

        var (items, total) = await repo.GetAllMatchingAsync(new EngineeringDocumentListFilter(), false, null, maxRows: 10);

        Assert.Equal(12, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task RegisterExport_NowCarriesIssuePurpose_ItWasAlwaysBlankBefore()
    {
        var repo = CreateRepo(nameof(RegisterExport_NowCarriesIssuePurpose_ItWasAlwaysBlankBefore), out var ctx);
        ctx.EngineeringDocuments.Add(Doc("MECH-1", 5, "Issued", issuePurpose: "IFA"));
        await ctx.SaveChangesAsync();

        var (items, _) = await repo.GetRegisterAllMatchingAsync(new DocumentRegisterFilter(), false, null, 100);

        Assert.Equal("IFA", items.Single().IssuePurpose);
    }

    [Fact]
    public void ExcelBuilder_WritesHeaderAndOneRowPerDocument_WithTheListColumns()
    {
        var items = new List<EngineeringDocumentDto>
        {
            new() { DocumentNo = "A-1", Title = "T1", DocumentType = "DWG", DisciplineName = "Mechanical", ProjectName = "P", RevisionNo = "01", Status = "Approved", IsFrozen = true, CreatedDate = new DateTime(2026, 1, 2) },
            new() { DocumentNo = "A-2", Title = "T2", DocumentType = "DSH", DisciplineName = "Electrical", ProjectName = "P", RevisionNo = "00", Status = "Draft", CreatedDate = new DateTime(2026, 1, 3) },
        };

        var bytes = EngineeringDocumentListExport.Build(items);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        Assert.Equal("Document No", ws.Cell(1, 1).GetString());
        Assert.Equal("Status", ws.Cell(1, 8).GetString());
        Assert.Equal(3, ws.LastRowUsed()!.RowNumber());
        Assert.Equal("A-1", ws.Cell(2, 1).GetString());
        Assert.Equal("Approved", ws.Cell(2, 8).GetString());
        Assert.Equal("Yes", ws.Cell(2, 10).GetString());
        Assert.Equal("2026-01-03", ws.Cell(3, 12).GetString());
    }
}
