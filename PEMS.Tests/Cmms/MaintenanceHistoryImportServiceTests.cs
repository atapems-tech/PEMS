using ClosedXML.Excel;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS CMMS — Data Migration &amp; Controlled Data Onboarding»: تست‌های Parsing، تطبیق
/// قطعی (Resolution)، Idempotency (کلید ترکیبی Equipment+Date+Action+Technician)، تایید
/// (Approval)، و Commit - عیناً همون الگوی EquipmentFunctionalLocationAssignmentServiceTests.
/// </summary>
public class MaintenanceHistoryImportServiceTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"MhImport_{dbName}");

    private static Stream BuildWorkbook(params (string Tag, string Date, string? WorkOrder, string? Action, string? Technician)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "Equipment Tag";
        sheet.Cell(1, 2).Value = "Execution Date";
        sheet.Cell(1, 3).Value = "Work Order Number";
        sheet.Cell(1, 4).Value = "Action Taken";
        sheet.Cell(1, 5).Value = "Technician";

        var r = 2;
        foreach (var (tag, date, wo, action, tech) in rows)
        {
            sheet.Cell(r, 1).Value = tag;
            sheet.Cell(r, 2).Value = date;
            sheet.Cell(r, 3).Value = wo ?? "";
            sheet.Cell(r, 4).Value = action ?? "";
            sheet.Cell(r, 5).Value = tech ?? "";
            r++;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static async Task<Equipment> SeedEquipmentAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        var eq = new Equipment { TagNumber = tag, Name = tag, EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();
        return eq;
    }

    private static async Task<WorkOrder> SeedWorkOrderAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string number, int equipmentId)
    {
        var wo = new WorkOrder { WorkOrderNumber = number, WorkOrderType = "Corrective", EquipmentId = equipmentId, Status = "Closed" };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    // ================= Source and Parsing =================

    [Fact]
    public async Task StageFromExcelAsync_ValidInput_StagesAsNew()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ValidInput_StagesAsNew));
        var eq = await SeedEquipmentAsync(ctx, "P-1001");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("P-1001", "2026-01-15", null, "Replaced seal", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("New", row.Status);
        Assert.True(row.IncludeInCommit);
        Assert.Equal("Valid", row.ValidationStatus);
    }

    [Fact]
    public async Task StageFromExcelAsync_MissingEquipmentTag_ValidationError()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_MissingEquipmentTag_ValidationError));
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("", "2026-01-15", null, "Replaced seal", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("ValidationError", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_InvalidDate_ValidationError()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_InvalidDate_ValidationError));
        await SeedEquipmentAsync(ctx, "P-1002");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("P-1002", "not-a-date", null, "Replaced seal", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("ValidationError", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_DuplicateSourceRowsInSameFile_SecondFlaggedDuplicate()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_DuplicateSourceRowsInSameFile_SecondFlaggedDuplicate));
        await SeedEquipmentAsync(ctx, "P-1003");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(
            ("P-1003", "2026-01-15", null, "Replaced seal", "Ali"),
            ("P-1003", "2026-01-15", null, "Replaced seal", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        Assert.Equal(2, batch!.Rows.Items.Count);
        Assert.Equal("New", batch.Rows.Items[0].Status);
        Assert.Equal("Duplicate", batch.Rows.Items[1].Status);
        Assert.False(batch.Rows.Items[1].IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_EmptyRowsSkippedSilently()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_EmptyRowsSkippedSilently));
        await SeedEquipmentAsync(ctx, "P-1004");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(
            ("P-1004", "2026-01-15", null, "Inspection", "Ali"),
            ("", "", null, null, null));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        Assert.Single(batch!.Rows.Items); // ردیف کاملاً خالی رد شد
    }

    // ================= Identity / Idempotency =================

    [Fact]
    public async Task StageFromExcelAsync_ExistingMaintenanceHistoryWithSameKey_FlaggedDuplicate()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ExistingMaintenanceHistoryWithSameKey_FlaggedDuplicate));
        var eq = await SeedEquipmentAsync(ctx, "P-1005");
        ctx.MaintenanceHistories.Add(new MaintenanceHistory
        {
            EquipmentId = eq.EquipmentId,
            ExecutionDate = new DateTime(2026, 1, 15),
            ActionTaken = "Replaced seal",
            Technician = "Ali",
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("P-1005", "2026-01-15", null, "Replaced seal", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("Duplicate", row.Status);
        Assert.False(row.IncludeInCommit);
        Assert.NotNull(row.DuplicateOfMaintenanceHistoryId);
    }

    [Fact]
    public async Task StageFromExcelAsync_AmbiguousEquipmentTag_ConflictEquipment()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_AmbiguousEquipmentTag_ConflictEquipment));
        ctx.Equipments.Add(new Equipment { TagNumber = "DUP-TAG", Name = "A", EquipmentType = "Pump", IsActive = true });
        ctx.Equipments.Add(new Equipment { TagNumber = "DUP-TAG", Name = "B", EquipmentType = "Pump", IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("DUP-TAG", "2026-01-15", null, "Inspection", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("ConflictEquipment", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    // ================= References =================

    [Fact]
    public async Task StageFromExcelAsync_UnresolvedEquipment_NotCommittable()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_UnresolvedEquipment_NotCommittable));
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("NO-SUCH-TAG", "2026-01-15", null, "Inspection", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("UnresolvedEquipment", row.Status);
        Assert.False(row.IncludeInCommit);
    }

    [Fact]
    public async Task StageFromExcelAsync_ValidWorkOrderNumber_Resolves()
    {
        var ctx = CreateContext(nameof(StageFromExcelAsync_ValidWorkOrderNumber_Resolves));
        var eq = await SeedEquipmentAsync(ctx, "P-1006");
        var wo = await SeedWorkOrderAsync(ctx, "WO-2026-0001", eq.EquipmentId);
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("P-1006", "2026-01-15", "WO-2026-0001", "Repair", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("New", row.Status);
        Assert.Equal("WO-2026-0001", row.WorkOrderNumber);
    }

    [Fact]
    public async Task StageFromExcelAsync_UnresolvedWorkOrder_IsWarningOnly_StillNewAndCommittable()
    {
        // طبق بند «WorkOrder Soft Reference»: هرگز مانع Commit نمی‌شود - فقط هشدار
        var ctx = CreateContext(nameof(StageFromExcelAsync_UnresolvedWorkOrder_IsWarningOnly_StillNewAndCommittable));
        await SeedEquipmentAsync(ctx, "P-1007");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("P-1007", "2026-01-15", "WO-DOES-NOT-EXIST", "Repair", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        var row = Assert.Single(batch!.Rows.Items);
        Assert.Equal("New", row.Status);
        Assert.True(row.IncludeInCommit);
        Assert.Equal("Warning", row.ValidationStatus);
        Assert.NotNull(row.ValidationMessage);
    }

    // ================= Review and Approval =================

    [Fact]
    public async Task SetRowIncludeAsync_ValidationErrorRow_CannotBeForcedToInclude()
    {
        var ctx = CreateContext(nameof(SetRowIncludeAsync_ValidationErrorRow_CannotBeForcedToInclude));
        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("", "2026-01-15", null, "x", "y"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);
        var rowId = batch!.Rows.Items[0].MaintenanceHistoryImportRowId;

        await service.SetRowIncludeAsync(rowId, true);

        var reloaded = await service.GetBatchAsync(batchId, 1, 50, null);
        Assert.False(reloaded!.Rows.Items[0].IncludeInCommit);
    }

    [Fact]
    public async Task SetRowIncludeAsync_DuplicateRow_CanBeExplicitlyIncludedByReviewer()
    {
        // طبق بند «Review»: تشخیص تکرار یک هشدار قابل‌بازبینی است، نه یک قفل غیرقابل‌عبور -
        // بازبین مجاز می‌تواند آگاهانه تصمیم بگیرد این مورد واقعاً تکراری نیست.
        var ctx = CreateContext(nameof(SetRowIncludeAsync_DuplicateRow_CanBeExplicitlyIncludedByReviewer));
        var eq = await SeedEquipmentAsync(ctx, "P-1008");
        ctx.MaintenanceHistories.Add(new MaintenanceHistory { EquipmentId = eq.EquipmentId, ExecutionDate = new DateTime(2026, 1, 15), ActionTaken = "Inspection", Technician = "Ali", IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("P-1008", "2026-01-15", null, "Inspection", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);
        var rowId = batch!.Rows.Items[0].MaintenanceHistoryImportRowId;

        await service.SetRowIncludeAsync(rowId, true);

        var reloaded = await service.GetBatchAsync(batchId, 1, 50, null);
        Assert.True(reloaded!.Rows.Items[0].IncludeInCommit);
    }

    // ================= Commit =================

    [Fact]
    public async Task CommitBatchAsync_NewRow_CreatesMaintenanceHistory()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_NewRow_CreatesMaintenanceHistory));
        await SeedEquipmentAsync(ctx, "P-2001");
        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("P-2001", "2026-01-15", null, "Replaced bearing", "Reza"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Created);
        Assert.Single(ctx.MaintenanceHistories);
        var created = ctx.MaintenanceHistories.Single();
        Assert.Equal("Replaced bearing", created.ActionTaken);
    }

    [Fact]
    public async Task CommitBatchAsync_RepeatedImportOfSameFile_SecondCommitCreatesNothing()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_RepeatedImportOfSameFile_SecondCommitCreatesNothing));
        await SeedEquipmentAsync(ctx, "P-2002");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream1 = BuildWorkbook(("P-2002", "2026-01-15", null, "Replaced bearing", "Reza"));
        var batch1Id = await service.StageFromExcelAsync(stream1, "test.xlsx", null);
        var result1 = await service.CommitBatchAsync(batch1Id);
        Assert.Equal(1, result1.Created);

        // همون فایل، دوباره Import می‌شود - طبق بند «Repeated Same Import»: هیچ رکورد تکراری نباید ساخته شود
        using var stream2 = BuildWorkbook(("P-2002", "2026-01-15", null, "Replaced bearing", "Reza"));
        var batch2Id = await service.StageFromExcelAsync(stream2, "test.xlsx", null);
        var batch2 = await service.GetBatchAsync(batch2Id, 1, 50, null);
        Assert.Equal("Duplicate", batch2!.Rows.Items[0].Status); // در همون مرحله‌ی Staging تشخیص داده شد

        var result2 = await service.CommitBatchAsync(batch2Id);

        Assert.Equal(0, result2.Created);
        Assert.Single(ctx.MaintenanceHistories); // فقط همون یک رکورد اول
    }

    [Fact]
    public async Task CommitBatchAsync_RecordBecameDuplicateBetweenStagingAndCommit_SkippedNotDuplicated()
    {
        // طبق بند «Idempotency در لحظه‌ی Commit»: یک Import دیگر ممکن است بین Staging و Commit
        // همین رکورد را ثبت کرده باشد - Commit باید دوباره بررسی کند، نه فقط به عکس‌فوری Staging تکیه کند.
        var ctx = CreateContext(nameof(CommitBatchAsync_RecordBecameDuplicateBetweenStagingAndCommit_SkippedNotDuplicated));
        var eq = await SeedEquipmentAsync(ctx, "P-2003");
        var service = new MaintenanceHistoryImportService(ctx);

        using var stream = BuildWorkbook(("P-2003", "2026-01-15", null, "Inspection", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        // بین Staging و Commit، همین رکورد از یک مسیر دیگر (مثلاً Batch دیگر) واقعاً ثبت می‌شود
        ctx.MaintenanceHistories.Add(new MaintenanceHistory { EquipmentId = eq.EquipmentId, ExecutionDate = new DateTime(2026, 1, 15), ActionTaken = "Inspection", Technician = "Ali", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(ctx.MaintenanceHistories); // هنوز فقط همون یک رکورد - نه دو
    }

    [Fact]
    public async Task CommitBatchAsync_ExcludedRow_NotCreated()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_ExcludedRow_NotCreated));
        await SeedEquipmentAsync(ctx, "P-2004");
        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("P-2004", "2026-01-15", null, "Inspection", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);
        var rowId = batch!.Rows.Items[0].MaintenanceHistoryImportRowId;

        await service.SetRowIncludeAsync(rowId, false);
        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(0, result.Created);
        Assert.Empty(ctx.MaintenanceHistories);
    }

    [Fact]
    public async Task CommitBatchAsync_PartialApproval_OnlyApprovedRowsCommitted()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_PartialApproval_OnlyApprovedRowsCommitted));
        await SeedEquipmentAsync(ctx, "P-2005");
        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(
            ("P-2005", "2026-01-15", null, "Inspection", "Ali"),
            ("P-2005", "2026-02-15", null, "Lubrication", "Reza"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);
        var batch = await service.GetBatchAsync(batchId, 1, 50, null);

        await service.SetRowIncludeAsync(batch!.Rows.Items[1].MaintenanceHistoryImportRowId, false);
        var result = await service.CommitBatchAsync(batchId);

        Assert.Equal(1, result.Created);
        Assert.Single(ctx.MaintenanceHistories);
        Assert.Equal("Inspection", ctx.MaintenanceHistories.Single().ActionTaken);
    }

    [Fact]
    public async Task CommitBatchAsync_NoOrphanReferences_WorkOrderIdOnlySetWhenResolved()
    {
        var ctx = CreateContext(nameof(CommitBatchAsync_NoOrphanReferences_WorkOrderIdOnlySetWhenResolved));
        var eq = await SeedEquipmentAsync(ctx, "P-2006");
        var wo = await SeedWorkOrderAsync(ctx, "WO-2026-0099", eq.EquipmentId);
        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("P-2006", "2026-01-15", "WO-2026-0099", "Repair", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        await service.CommitBatchAsync(batchId);

        var created = ctx.MaintenanceHistories.Single();
        Assert.Equal(wo.WorkOrderId, created.WorkOrderId);
    }

    [Fact]
    public async Task DiscardBatchAsync_RemovesBatchAndRows_NoMaintenanceHistoryCreated()
    {
        var ctx = CreateContext(nameof(DiscardBatchAsync_RemovesBatchAndRows_NoMaintenanceHistoryCreated));
        await SeedEquipmentAsync(ctx, "P-2007");
        var service = new MaintenanceHistoryImportService(ctx);
        using var stream = BuildWorkbook(("P-2007", "2026-01-15", null, "Inspection", "Ali"));
        var batchId = await service.StageFromExcelAsync(stream, "test.xlsx", null);

        await service.DiscardBatchAsync(batchId);

        Assert.Empty(ctx.MaintenanceHistoryImportBatches);
        Assert.Empty(ctx.MaintenanceHistoryImportRows);
        Assert.Empty(ctx.MaintenanceHistories);
    }
}
