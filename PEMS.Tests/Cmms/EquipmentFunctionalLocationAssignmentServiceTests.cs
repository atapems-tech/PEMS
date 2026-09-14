using ClosedXML.Excel;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS CMMS — Equipment Functional Location Assignment &amp; Import»: تست‌های تطبیق
/// قطعی (Resolution)، تایید (Approval)، محافظت از تخصیص موجود، و Idempotency - عیناً همون
/// الگوی FunctionalLocationImportServiceTests.
/// </summary>
public class EquipmentFunctionalLocationAssignmentServiceTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"EqFlAssign_{dbName}");

    private static Stream BuildWorkbook(params (string Tag, string Code)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "Equipment Tag";
        sheet.Cell(1, 2).Value = "Functional Location Code";

        var r = 2;
        foreach (var (tag, code) in rows)
        {
            sheet.Cell(r, 1).Value = tag;
            sheet.Cell(r, 2).Value = code;
            r++;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static async Task<Equipment> SeedEquipmentAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag, int? functionalLocationId = null)
    {
        var eq = new Equipment { TagNumber = tag, Name = tag, EquipmentType = "Pump", IsActive = true, FunctionalLocationId = functionalLocationId };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();
        return eq;
    }

    private static async Task<FunctionalLocation> SeedFlAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string code)
    {
        var fl = new FunctionalLocation { Code = code, Name = code, IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();
        return fl;
    }

    // ================= Resolution =================

    [Fact]
    public async Task StageFromExcelAsync_ExactTagAndCode_ResolvesAsExact()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ExactTagAndCode_ResolvesAsExact));
        await SeedEquipmentAsync(ctx, "P-1001");
        var fl = await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("Exact", row.Status);
        Assert.True(row.IncludeInCommit);
        Assert.Equal(fl.FunctionalLocationId, row.ResolvedFunctionalLocationId);
    }

    [Fact]
    public async Task StageFromExcelAsync_EquipmentNotFound_UnresolvedEquipment()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_EquipmentNotFound_UnresolvedEquipment));
        await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("GHOST-TAG", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("UnresolvedEquipment", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_FunctionalLocationNotFound_UnresolvedFunctionalLocation()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_FunctionalLocationNotFound_UnresolvedFunctionalLocation));
        await SeedEquipmentAsync(ctx, "P-1001");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "GHOST-CODE"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("UnresolvedFunctionalLocation", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_DuplicateEquipmentTag_ConflictEquipment()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_DuplicateEquipmentTag_ConflictEquipment));
        await SeedEquipmentAsync(ctx, "P-1001");
        await SeedEquipmentAsync(ctx, "P-1001"); // Tag تکراری - بدون محدودیت Uniqueness در Schema
        await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("ConflictEquipment", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_DuplicateFunctionalLocationCode_ConflictFunctionalLocation()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_DuplicateFunctionalLocationCode_ConflictFunctionalLocation));
        await SeedEquipmentAsync(ctx, "P-1001");
        await SeedFlAsync(ctx, "85A07");
        await SeedFlAsync(ctx, "85A07"); // Code تکراری - بدون محدودیت Uniqueness در Schema
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("ConflictFunctionalLocation", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_EmptyRequiredField_ValidationError()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_EmptyRequiredField_ValidationError));
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", ""));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("ValidationError", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_EquipmentAlreadyHasSameFl_NoChange()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_EquipmentAlreadyHasSameFl_NoChange));
        var fl = await SeedFlAsync(ctx, "85A07");
        await SeedEquipmentAsync(ctx, "P-1001", fl.FunctionalLocationId);
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("NoChange", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_EquipmentAlreadyHasDifferentFl_AlreadyAssigned_ProtectedByDefault()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_EquipmentAlreadyHasDifferentFl_AlreadyAssigned_ProtectedByDefault));
        var oldFl = await SeedFlAsync(ctx, "OLD-LOC");
        var newFl = await SeedFlAsync(ctx, "85A07");
        await SeedEquipmentAsync(ctx, "P-1001", oldFl.FunctionalLocationId);
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("AlreadyAssigned", row.Status);
        Assert.False(row.IncludeInCommit); // طبق «Existing Assignment Protection»: پیش‌فرض محافظت
        Assert.False(row.ReplaceExistingAssignment);
        Assert.Equal(newFl.FunctionalLocationId, row.ResolvedFunctionalLocationId);
    }

    // ================= Approval / Commit =================

    [Fact]
    public async Task CommitBatchAsync_ExactRow_AssignsEquipment()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_ExactRow_AssignsEquipment));
        var eq = await SeedEquipmentAsync(ctx, "P-1001");
        var fl = await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Assigned);
        var updated = ctx.Equipments.Single(x => x.EquipmentId == eq.EquipmentId);
        Assert.Equal(fl.FunctionalLocationId, updated.FunctionalLocationId);
    }

    [Fact]
    public async Task CommitBatchAsync_UnresolvedRow_NeverCommitted_EvenIfIncludeForced()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_UnresolvedRow_NeverCommitted_EvenIfIncludeForced));
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("GHOST", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batchBefore = await service.GetBatchAsync(batchId, 1, 50, null);
        var rowId = batchBefore!.Rows.Items[0].EquipmentFunctionalLocationAssignmentRowId;

        // طبق «Do not commit invalid rows»: تلاش برای روشن‌کردن اجباری یک ردیف Unresolved
        await service.SetRowIncludeAsync(rowId, true);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(0, result.Assigned);
        Assert.Equal(1, result.Unresolved);
        Assert.Empty(ctx.Equipments.Where(x => x.FunctionalLocationId != null));
    }

    [Fact]
    public async Task CommitBatchAsync_AlreadyAssignedRow_NotReplacedWithoutExplicitApproval()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_AlreadyAssignedRow_NotReplacedWithoutExplicitApproval));
        var oldFl = await SeedFlAsync(ctx, "OLD-LOC");
        var newFl = await SeedFlAsync(ctx, "85A07");
        var eq = await SeedEquipmentAsync(ctx, "P-1001", oldFl.FunctionalLocationId);
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(0, result.Assigned);
        var unchanged = ctx.Equipments.Single(x => x.EquipmentId == eq.EquipmentId);
        Assert.Equal(oldFl.FunctionalLocationId, unchanged.FunctionalLocationId); // دست‌نخورده ماند
    }

    [Fact]
    public async Task CommitBatchAsync_AlreadyAssignedRow_ReplacedOnlyAfterExplicitApproval()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_AlreadyAssignedRow_ReplacedOnlyAfterExplicitApproval));
        var oldFl = await SeedFlAsync(ctx, "OLD-LOC");
        var newFl = await SeedFlAsync(ctx, "85A07");
        var eq = await SeedEquipmentAsync(ctx, "P-1001", oldFl.FunctionalLocationId);
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batchBefore = await service.GetBatchAsync(batchId, 1, 50, null);
        var rowId = batchBefore!.Rows.Items[0].EquipmentFunctionalLocationAssignmentRowId;

        await service.SetReplaceExistingAssignmentAsync(rowId, true);
        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Assigned);
        var updated = ctx.Equipments.Single(x => x.EquipmentId == eq.EquipmentId);
        Assert.Equal(newFl.FunctionalLocationId, updated.FunctionalLocationId);
    }

    [Fact]
    public async Task CommitBatchAsync_PartialApproval_OnlyIncludedRowsCommitted()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_PartialApproval_OnlyIncludedRowsCommitted));
        var fl = await SeedFlAsync(ctx, "85A07");
        await SeedEquipmentAsync(ctx, "P-1001");
        await SeedEquipmentAsync(ctx, "P-1002");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"), ("P-1002", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batchBefore = await service.GetBatchAsync(batchId, 1, 50, null);

        // طبق «Partial Approval»: فقط یکی از دو ردیف معتبر را از Commit خارج کن
        await service.SetRowIncludeAsync(batchBefore!.Rows.Items[1].EquipmentFunctionalLocationAssignmentRowId, false);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Assigned);
        Assert.Equal(1, ctx.Equipments.Count(x => x.FunctionalLocationId == fl.FunctionalLocationId));
    }

    // ================= Idempotency =================

    [Fact]
    public async Task CommitBatchAsync_RepeatedImport_SecondRunIsNoOp()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_RepeatedImport_SecondRunIsNoOp));
        var eq = await SeedEquipmentAsync(ctx, "P-1001");
        var fl = await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using (var stream1 = BuildWorkbook(("P-1001", "85A07")))
        {
            var batchId1 = await service.StageFromExcelAsync(stream1, "test.xlsx", null);
            var result1 = await service.CommitBatchAsync(batchId1);
            Assert.Equal(1, result1.Assigned);
        }

        using (var stream2 = BuildWorkbook(("P-1001", "85A07")))
        {
            var batchId2 = await service.StageFromExcelAsync(stream2, "test.xlsx", null);
            var batch2 = await service.GetBatchAsync(batchId2, 1, 50, null);

            // دومین بار، همین ردیف باید NoChange باشد - نه Exact دوباره
            Assert.Equal("NoChange", batch2!.Rows.Items[0].Status);

            var result2 = await service.CommitBatchAsync(batchId2);
            Assert.Equal(0, result2.Assigned);
        }

        Assert.Equal(fl.FunctionalLocationId, ctx.Equipments.Single(x => x.EquipmentId == eq.EquipmentId).FunctionalLocationId);
    }

    // ================= Discard =================

    [Fact]
    public async Task DiscardBatchAsync_RemovesStagingRows_DoesNotTouchEquipment()
    {
        var ctx = CreateContext(nameof(DiscardBatchAsync_RemovesStagingRows_DoesNotTouchEquipment));
        var eq = await SeedEquipmentAsync(ctx, "P-1001");
        await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        await service.DiscardBatchAsync(batchId);

        Assert.Empty(ctx.EquipmentFunctionalLocationAssignmentBatches);
        Assert.Empty(ctx.EquipmentFunctionalLocationAssignmentRows);
        Assert.Null(ctx.Equipments.Single(x => x.EquipmentId == eq.EquipmentId).FunctionalLocationId);
    }

    // ================= Audit =================

    [Fact]
    public async Task CommitBatchAsync_CommittedAssignment_CreatesAuditLogEntry()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_CommittedAssignment_CreatesAuditLogEntry));
        var eq = await SeedEquipmentAsync(ctx, "P-1001");
        var fl = await SeedFlAsync(ctx, "85A07");
        var service = new EquipmentFunctionalLocationAssignmentService(ctx);

        using var stream = BuildWorkbook(("P-1001", "85A07"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        await service.CommitBatchAsync(batchId);

        // طبق بند «Audit Trail»: بدون کد Audit دستی - فقط Interceptor سراسری
        // PEMSDbContext.SaveChangesAsync، که برای هر Update واقعی روی Equipment یک AuditLog می‌سازد.
        var audit = ctx.AuditLogs.Where(x => x.EntityName == "Equipment" && x.EntityKeyValue == eq.EquipmentId.ToString()).ToList();
        Assert.Contains(audit, a => a.Action == "Update" && a.ChangedFieldsSummary != null && a.ChangedFieldsSummary.Contains("FunctionalLocationId"));
    }
}
