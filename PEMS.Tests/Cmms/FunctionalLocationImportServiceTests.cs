using ClosedXML.Excel;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «Functional Location Master Data Import»: تست‌های تجزیه (Parsing) و ایمپورت (Import) -
/// طبق بند ۲۱/۲۲ سند مستر: تک‌کد، چندکد، خالی، فاصله، تکراری‌درون‌فایل، تناقض، رکورد جدید،
/// رکورد موجود، ایمپورت دوباره‌ی Idempotent (بدون تکرار)، و صحت داده پس از Commit.
/// </summary>
public class FunctionalLocationImportServiceTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"FLImport_{dbName}");

    private static Stream BuildWorkbook(params (string AreaName, string AreaCode)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "";
        sheet.Cell(1, 2).Value = "AREA NAME";
        sheet.Cell(1, 3).Value = "AREA CODE";

        var r = 2;
        foreach (var (areaName, areaCode) in rows)
        {
            sheet.Cell(r, 2).Value = areaName;
            sheet.Cell(r, 3).Value = areaCode;
            r++;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    // ================= Parsing =================

    [Fact]
    public async Task StageFromExcelAsync_SingleCode_CreatesOneRow_NoAreaParent()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_SingleCode_CreatesOneRow_NoAreaParent));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.NotNull(batch);
        Assert.Single(batch!.Rows);
        Assert.Equal("88A07", batch.Rows[0].Code);
        Assert.Equal("COOLING TOWER", batch.Rows[0].Name);
        Assert.Null(batch.Rows[0].ParentAreaName);
        Assert.Equal("New", batch.Rows[0].Status);
        Assert.Equal("Create", batch.Rows[0].Action);
    }

    [Fact]
    public async Task StageFromExcelAsync_MultipleCodes_ExpandsIntoParentPlusChildren()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_MultipleCodes_ExpandsIntoParentPlusChildren));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("FINISHING", "85A07, 85A08, 85A10, 85A12"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.NotNull(batch);
        // ۱ ردیف والد + ۴ ردیف فرزند = ۵ ردیف Staging از یک سطر منبع
        Assert.Equal(5, batch!.Rows.Count);
        Assert.Equal(1, batch.SourceRowCount);
        Assert.Equal(5, batch.ExpandedLocationCount);

        var parent = batch.Rows.Single(x => x.Payload!.IsAreaParentRow);
        Assert.Equal("FINISHING", parent.Code);
        Assert.Null(parent.ParentAreaName);

        var children = batch.Rows.Where(x => !x.Payload!.IsAreaParentRow).ToList();
        Assert.Equal(4, children.Count);
        Assert.All(children, c => Assert.Equal("FINISHING", c.ParentAreaName));
        Assert.Equal(new[] { "85A07", "85A08", "85A10", "85A12" }, children.Select(x => x.Code).OrderBy(x => x));
    }

    [Fact]
    public async Task StageFromExcelAsync_CodeNotModified_PreservesExactCasingAndFormat()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_CodeNotModified_PreservesExactCasingAndFormat));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("BAGGING SYSTEM", "  80A04  "));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        // فقط فاصله‌ی اضافه Trim می‌شود - خودِ کد دست‌نخورده می‌ماند (بند ۲۵)
        Assert.Equal("80A04", batch!.Rows[0].Code);
    }

    [Fact]
    public async Task StageFromExcelAsync_BlankAreaCode_MarkedInvalid()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_BlankAreaCode_MarkedInvalid));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("SOME AREA", ""));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.NotNull(batch);
        Assert.Single(batch!.Rows);
        Assert.Equal("Invalid", batch.Rows[0].Status);
        Assert.Equal("Reject", batch.Rows[0].Action);
        Assert.False(batch.Rows[0].IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_BlankAreaName_MarkedInvalid()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_BlankAreaName_MarkedInvalid));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("", "85A99"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.NotNull(batch);
        Assert.Single(batch!.Rows);
        Assert.Equal("Invalid", batch.Rows[0].Status);
    }

    [Fact]
    public async Task StageFromExcelAsync_TotallyBlankRow_SilentlySkipped_NotCountedAsInvalid()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_TotallyBlankRow_SilentlySkipped_NotCountedAsInvalid));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("", ""), ("COOLING TOWER", "88A07"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        // ردیف کاملاً خالی هیچ ردیف Staging ای تولید نمی‌کند - فقط سطر واقعی
        Assert.Single(batch!.Rows);
        Assert.Equal("88A07", batch.Rows[0].Code);
    }

    [Fact]
    public async Task StageFromExcelAsync_SameCodeSameAreaName_TwiceInFile_MarkedDuplicateWarning()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_SameCodeSameAreaName_TwiceInFile_MarkedDuplicateWarning));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"), ("COOLING TOWER", "88A07"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.Equal(2, batch!.Rows.Count);
        Assert.Equal("New", batch.Rows[0].Status);
        Assert.Equal("Conflict", batch.Rows[1].Status);
        Assert.False(batch.Rows[1].IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_SameCodeDifferentAreaName_MarkedConflict()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_SameCodeDifferentAreaName_MarkedConflict));
        var service = new FunctionalLocationImportService(ctx);
        // داده‌ی واقعی فایل نمونه: LOCAL ROOM 4000 و LOCAL ROOM 5000 هر دو 80A10 دارند
        using var stream = BuildWorkbook(("LOCAL ROOM 4000", "80A10"), ("LOCAL ROOM 5000", "80A10"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.Equal(2, batch!.Rows.Count);
        Assert.Equal("New", batch.Rows[0].Status);
        Assert.Equal("Conflict", batch.Rows[1].Status);
        Assert.False(batch.Rows[1].IncludeInCommit);
        Assert.Contains("تناقض", batch.Rows[1].ValidationMessage);
    }

    [Fact]
    public async Task StageFromExcelAsync_ExistingIdenticalRecord_MarkedExistingSkip()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ExistingIdenticalRecord_MarkedExistingSkip));
        ctx.FunctionalLocations.Add(new FunctionalLocation { Code = "88A07", Name = "COOLING TOWER", Area = "COOLING TOWER", IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.Equal("Existing", batch!.Rows[0].Status);
        Assert.Equal("Skip", batch.Rows[0].Action);
        Assert.False(batch.Rows[0].IncludeInCommit);
        Assert.True(batch.Rows[0].IsUpdate);
    }

    [Fact]
    public async Task StageFromExcelAsync_ExistingDifferentData_MarkedConflict()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ExistingDifferentData_MarkedConflict));
        ctx.FunctionalLocations.Add(new FunctionalLocation { Code = "88A07", Name = "OLD NAME", Area = "OLD NAME", IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"));

        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId);

        Assert.Equal("Conflict", batch!.Rows[0].Status);
        Assert.False(batch.Rows[0].IncludeInCommit);
    }

    // ================= Import / Commit =================

    [Fact]
    public async Task CommitBatchAsync_NewSingleCode_CreatesFunctionalLocation()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_NewSingleCode_CreatesFunctionalLocation));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        var fl = Assert.Single(ctx.FunctionalLocations);
        Assert.Equal("88A07", fl.Code);
        Assert.Equal("COOLING TOWER", fl.Name);
        Assert.Null(fl.ParentId);
    }

    [Fact]
    public async Task CommitBatchAsync_MultiCode_CreatesParentAndChildrenWithCorrectParentId()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_MultiCode_CreatesParentAndChildrenWithCorrectParentId));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("FINISHING", "85A07, 85A08, 85A10, 85A12"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(5, result.Created); // ۱ والد + ۴ فرزند
        var parent = ctx.FunctionalLocations.Single(x => x.Code == "FINISHING");
        Assert.Null(parent.ParentId);

        var children = ctx.FunctionalLocations.Where(x => x.Code != "FINISHING").ToList();
        Assert.Equal(4, children.Count);
        Assert.All(children, c => Assert.Equal(parent.FunctionalLocationId, c.ParentId));
    }

    [Fact]
    public async Task CommitBatchAsync_ConflictRowsNotIncludedByDefault_AreNotCommitted()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_ConflictRowsNotIncludedByDefault_AreNotCommitted));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("LOCAL ROOM 4000", "80A10"), ("LOCAL ROOM 5000", "80A10"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Conflicts);
        Assert.Single(ctx.FunctionalLocations); // فقط اولین ادعا Commit شد
    }

    [Fact]
    public async Task CommitBatchAsync_TwiceOnSameData_IsIdempotent_NoDuplicatesCreated()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_TwiceOnSameData_IsIdempotent_NoDuplicatesCreated));
        var service = new FunctionalLocationImportService(ctx);

        using (var stream1 = BuildWorkbook(("FINISHING", "85A07, 85A08"), ("COOLING TOWER", "88A07")))
        {
            var batchId1 = await service.StageFromExcelAsync(stream1, "test.xlsx", null);
            var result1 = await service.CommitBatchAsync(batchId1);
            Assert.Equal(4, result1.Created); // FINISHING (parent) + 85A07 + 85A08 + 88A07
        }

        var countAfterFirstImport = ctx.FunctionalLocations.Count(x => x.IsActive);

        using (var stream2 = BuildWorkbook(("FINISHING", "85A07, 85A08"), ("COOLING TOWER", "88A07")))
        {
            var batchId2 = await service.StageFromExcelAsync(stream2, "test.xlsx", null);
            var result2 = await service.CommitBatchAsync(batchId2);

            Assert.Equal(0, result2.Created);
            Assert.Equal(0, result2.Updated);
        }

        var countAfterSecondImport = ctx.FunctionalLocations.Count(x => x.IsActive);
        Assert.Equal(countAfterFirstImport, countAfterSecondImport);
    }

    [Fact]
    public async Task DiscardBatchAsync_RemovesStagingRows_DoesNotTouchFunctionalLocations()
    {
        var ctx = CreateContext(nameof(DiscardBatchAsync_RemovesStagingRows_DoesNotTouchFunctionalLocations));
        var service = new FunctionalLocationImportService(ctx);
        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        await service.DiscardBatchAsync(batchId);

        Assert.Empty(ctx.FunctionalLocationImportBatches);
        Assert.Empty(ctx.FunctionalLocationImportRows);
        Assert.Empty(ctx.FunctionalLocations);
    }

    [Fact]
    public async Task SetRowIncludeAsync_TogglesIncludeFlag()
    {
        var ctx = CreateContext(nameof(SetRowIncludeAsync_TogglesIncludeFlag));
        var service = new FunctionalLocationImportService(ctx);
        ctx.FunctionalLocations.Add(new FunctionalLocation { Code = "88A07", Name = "COOLING TOWER", Area = "COOLING TOWER", IsActive = true });
        await ctx.SaveChangesAsync();

        using var stream = BuildWorkbook(("COOLING TOWER", "88A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batchBefore = await service.GetBatchAsync(batchId);
        Assert.False(batchBefore!.Rows[0].IncludeInCommit); // Existing/Skip به‌طور پیش‌فرض خاموش است

        await service.SetRowIncludeAsync(batchBefore.Rows[0].FunctionalLocationImportRowId, true);

        var batchAfter = await service.GetBatchAsync(batchId);
        Assert.True(batchAfter!.Rows[0].IncludeInCommit);
    }
}
