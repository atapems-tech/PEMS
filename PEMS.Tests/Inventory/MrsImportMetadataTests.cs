using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق سند «Material Master Import — Excel Metadata Extraction»: این تست‌ها دقیقاً روی
/// MrsImportService واقعی اجرا می‌شوند (نه شبیه‌سازی) - یک فایل Excel واقعی با ClosedXML در
/// حافظه ساخته می‌شود (نه فایل نمونه روی دیسک)، دقیقاً همان مسیر Stage→Review→Commit که
/// production استفاده می‌کند. InMemory کافی است چون این سرویس به SQL خام وابسته نیست.
/// </summary>
public class MrsImportMetadataTests
{
    private static MemoryStream BuildWorkbook(string[] headers, params string[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");

        for (int c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c];

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static (MrsImportService svc, PEMS.Persistence.Context.PEMSDbContext ctx) Seed(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory(dbName);
        var vendorService = new VendorService(ctx);
        return (new MrsImportService(ctx, vendorService), ctx);
    }

    // ==================== Test 1 — Header Discovery ====================
    [Fact]
    public async Task StageFromExcelAsync_EightColumnExcel_AllEightHeadersPersisted()
    {
        var (svc, ctx) = Seed(nameof(StageFromExcelAsync_EightColumnExcel_AllEightHeadersPersisted));

        var headers = new[] { "Code Rahkaran", "DESCRIPTION PACKING LIST", "VENDOR", "P/O NO.", "MRS NO", "unit", "QTY", "LOCATION" };
        using var wb = BuildWorkbook(headers, new[] { "MAT-001", "Ball Valve 2 inch", "ABC Engineering", "PO-2026-104", "WH-A-0026", "PCS", "10", "WH-A-03" });

        var (success, error, batchId) = await svc.StageFromExcelAsync(wb, "test.xlsx", null, null);

        Assert.True(success, error);
        var row = ctx.MrsImportRows.Include(x => x.Metadata).Single(x => x.MrsImportBatchId == batchId);
        Assert.Equal(8, row.Metadata.Count);
        Assert.Equal(headers.OrderBy(h => h), row.Metadata.Select(m => m.OriginalHeader).OrderBy(h => h));
    }

    // ==================== Test 2 — Vendor Metadata ====================
    [Fact]
    public async Task StageFromExcelAsync_VendorNameHeader_CreatesVendorNameMetadataLinkedToCorrectRow()
    {
        var (svc, ctx) = Seed(nameof(StageFromExcelAsync_VendorNameHeader_CreatesVendorNameMetadataLinkedToCorrectRow));

        var headers = new[] { "DESCRIPTION PACKING LIST", "Vendor Name" };
        using var wb = BuildWorkbook(headers, new[] { "Gasket 4 inch", "ABC Engineering Ltd." });

        var (success, _, batchId) = await svc.StageFromExcelAsync(wb, "test.xlsx", null, null);
        Assert.True(success);

        var row = ctx.MrsImportRows.Include(x => x.Metadata).Single(x => x.MrsImportBatchId == batchId);
        var vendorMeta = row.Metadata.Single(m => m.CanonicalField == "VendorName");

        Assert.Equal("Vendor Name", vendorMeta.OriginalHeader);
        Assert.Equal("ABC Engineering Ltd.", vendorMeta.Value);
        Assert.Equal(row.MrsImportRowId, vendorMeta.MrsImportRowId);
    }

    // ==================== Test 3 — Purchase Metadata ====================
    [Fact]
    public async Task StageFromExcelAsync_PurchaseNumberHeader_CreatesPurchaseOrderNumberMetadata()
    {
        var (svc, ctx) = Seed(nameof(StageFromExcelAsync_PurchaseNumberHeader_CreatesPurchaseOrderNumberMetadata));

        var headers = new[] { "DESCRIPTION PACKING LIST", "P/O NO." };
        using var wb = BuildWorkbook(headers, new[] { "Bearing SKF 6205", "PO-2026-00451" });

        var (success, _, batchId) = await svc.StageFromExcelAsync(wb, "test.xlsx", null, null);
        Assert.True(success);

        var row = ctx.MrsImportRows.Include(x => x.Metadata).Single(x => x.MrsImportBatchId == batchId);
        var poMeta = row.Metadata.Single(m => m.CanonicalField == "PurchaseOrderNumber");

        Assert.Equal("P/O NO.", poMeta.OriginalHeader);
        Assert.Equal("PO-2026-00451", poMeta.Value);

        // طبق FieldAliases موجود: باید هم‌زمان روی فیلد Typed قدیمی هم بنشیند - نه فقط Metadata
        Assert.Equal("PO-2026-00451", row.RawPoNo);
    }

    // ==================== Test 4 — Header Variations ====================
    [Theory]
    [InlineData("Vendor", "VendorName")]
    [InlineData("Vendor Name", "VendorName")]
    [InlineData("Supplier", "VendorName")]
    [InlineData("Supplier Name", "VendorName")]
    [InlineData("VENDOR", "VendorName")]
    [InlineData("vendor name", "VendorName")]
    [InlineData("Purchase Number", "PurchaseOrderNumber")]
    [InlineData("PO Number", "PurchaseOrderNumber")]
    [InlineData("Purchase No", "PurchaseOrderNumber")]
    [InlineData("PO No", "PurchaseOrderNumber")]
    [InlineData("P/O NO.", "PurchaseOrderNumber")]
    [InlineData("Wight", "Weight")]
    [InlineData("Weight", "Weight")]
    public void MrsMetadataCanonicalMap_ResolvesKnownVariations(string header, string? expectedCanonical)
    {
        Assert.Equal(expectedCanonical, MrsMetadataCanonicalMap.Resolve(header));
    }

    [Fact]
    public void MrsMetadataCanonicalMap_CaseOnlyCollision_QtyCheckVsQtyCHECK_ResolveDifferently()
    {
        // طبق سند: فایل واقعی هر دو "QTY check" و "QTY CHECK" را به‌عنوان دو ستون جدا دارد -
        // نباید بی‌صدا یکی شوند
        Assert.Equal("QtyCheck", MrsMetadataCanonicalMap.Resolve("QTY check"));
        Assert.Equal("QtyCheck2", MrsMetadataCanonicalMap.Resolve("QTY check2"));
        Assert.Equal("CheckedQuantity", MrsMetadataCanonicalMap.Resolve("QTY CHECK"));
    }

    // ==================== Test 5 — Unknown Headers ====================
    [Fact]
    public async Task StageFromExcelAsync_UnknownColumn_NotDiscarded_PreservedWithNullCanonicalField()
    {
        var (svc, ctx) = Seed(nameof(StageFromExcelAsync_UnknownColumn_NotDiscarded_PreservedWithNullCanonicalField));

        var headers = new[] { "DESCRIPTION PACKING LIST", "Warehouse Bin", "temp unique" };
        using var wb = BuildWorkbook(headers, new[] { "O-Ring 10mm", "B-14-03", "xyz-99" });

        var (success, _, batchId) = await svc.StageFromExcelAsync(wb, "test.xlsx", null, null);
        Assert.True(success);

        var row = ctx.MrsImportRows.Include(x => x.Metadata).Single(x => x.MrsImportBatchId == batchId);

        var binMeta = row.Metadata.Single(m => m.OriginalHeader == "Warehouse Bin");
        Assert.Null(binMeta.CanonicalField);
        Assert.Equal("B-14-03", binMeta.Value);

        var tempMeta = row.Metadata.Single(m => m.OriginalHeader == "temp unique");
        Assert.Null(tempMeta.CanonicalField);
        Assert.Equal("xyz-99", tempMeta.Value);
    }

    // ==================== Test 6 — Source Traceability ====================
    [Fact]
    public async Task FullChain_MaterialToMetadataToImportBatchToSourceDocument_IsTraceable()
    {
        var (svc, ctx) = Seed(nameof(FullChain_MaterialToMetadataToImportBatchToSourceDocument_IsTraceable));

        var attachment = new DocumentAttachment
        {
            FileName = "inventory.xlsx", FileExtension = ".xlsx", FilePath = "/uploads/mrs-import/x.xlsx",
            FileSize = 1000, IsActive = true, UploadDate = DateTime.Now, AIStatus = "NotApplicable"
        };
        ctx.DocumentAttachments.Add(attachment);
        await ctx.SaveChangesAsync();

        var headers = new[] { "DESCRIPTION PACKING LIST", "VENDOR" };
        using var wb = BuildWorkbook(headers, new[] { "Pressure Gauge 0-10 bar", "XYZ Instruments" });

        var (success, _, batchId) = await svc.StageFromExcelAsync(wb, "inventory.xlsx", null, null, attachment.AttachmentId);
        Assert.True(success);

        var row = ctx.MrsImportRows.Single(x => x.MrsImportBatchId == batchId);
        var (commitSuccess, commitError, committed, _) = await svc.CommitAsync(batchId!.Value, new List<int> { row.MrsImportRowId }, null);
        Assert.True(commitSuccess, commitError);
        Assert.Equal(1, committed);

        // زنجیره‌ی کامل: Material -> Row -> Batch -> Attachment
        var reloadedRow = ctx.MrsImportRows.Single(x => x.MrsImportRowId == row.MrsImportRowId);
        Assert.NotNull(reloadedRow.ResultMaterialItemId);

        var material = ctx.MaterialItems.Single(x => x.MaterialItemId == reloadedRow.ResultMaterialItemId);
        Assert.Equal("Pressure Gauge 0-10 bar", material.Description);
        Assert.NotNull(material.VendorId);

        var vendor = ctx.Vendors.Single(x => x.VendorId == material.VendorId);
        Assert.Equal("XYZ Instruments", vendor.Name);

        var batch = ctx.MrsImportBatches.Single(x => x.MrsImportBatchId == batchId);
        Assert.Equal(attachment.AttachmentId, batch.SourceAttachmentId);

        var meta = ctx.MrsImportRowMetadata.Where(x => x.MrsImportRowId == row.MrsImportRowId).ToList();
        Assert.Contains(meta, m => m.CanonicalField == "VendorName" && m.Value == "XYZ Instruments");
    }

    // ==================== Test 7 — Re-import ====================
    [Fact]
    public async Task StageFromExcelAsync_SameFileImportedTwice_CreatesSeparateBatchesWithOwnMetadata_SecondRowMatchesExistingMaterial()
    {
        var (svc, ctx) = Seed(nameof(StageFromExcelAsync_SameFileImportedTwice_CreatesSeparateBatchesWithOwnMetadata_SecondRowMatchesExistingMaterial));

        var headers = new[] { "DESCRIPTION PACKING LIST", "VENDOR" };

        using var wb1 = BuildWorkbook(headers, new[] { "Coupling Flexible 50mm", "Vendor A" });
        var (s1, _, batch1Id) = await svc.StageFromExcelAsync(wb1, "f.xlsx", null, null);
        Assert.True(s1);
        var row1 = ctx.MrsImportRows.Single(x => x.MrsImportBatchId == batch1Id);
        await svc.CommitAsync(batch1Id!.Value, new List<int> { row1.MrsImportRowId }, null);

        using var wb2 = BuildWorkbook(headers, new[] { "Coupling Flexible 50mm", "Vendor A" });
        var (s2, _, batch2Id) = await svc.StageFromExcelAsync(wb2, "f.xlsx", null, null);
        Assert.True(s2);

        Assert.NotEqual(batch1Id, batch2Id);

        var row2 = ctx.MrsImportRows.Single(x => x.MrsImportBatchId == batch2Id);
        // طبق تطبیق موجود سه‌لایه: دومین بار همون شرح، باید MatchedExisting شود، نه New
        Assert.Equal("MatchedExisting", row2.MatchStatus);

        // Metadata هر Batch مستقل باقی می‌ماند - رونویسی نمی‌شود
        var metaBatch1 = ctx.MrsImportRowMetadata.Where(x => x.MrsImportRowId == row1.MrsImportRowId).ToList();
        var metaBatch2 = ctx.MrsImportRowMetadata.Where(x => x.MrsImportRowId == row2.MrsImportRowId).ToList();
        Assert.NotEmpty(metaBatch1);
        Assert.NotEmpty(metaBatch2);
        Assert.NotEqual(metaBatch1.Select(m => m.MrsImportRowMetadataId), metaBatch2.Select(m => m.MrsImportRowMetadataId));

        // فقط یک MaterialItem واقعی باید ساخته شده باشد (نه یکی به‌ازای هر Import)
        Assert.Single(ctx.MaterialItems.Where(x => x.Description == "Coupling Flexible 50mm"));
    }

    // ==================== Test 8 — Transaction Safety ====================
    [Fact]
    public async Task StageFromExcelAsync_Success_BatchTotalRowsMatchesActuallyPersistedRows_NoOrphans()
    {
        var (svc, ctx) = Seed(nameof(StageFromExcelAsync_Success_BatchTotalRowsMatchesActuallyPersistedRows_NoOrphans));

        var headers = new[] { "DESCRIPTION PACKING LIST", "VENDOR" };
        using var wb = BuildWorkbook(headers,
            new[] { "Item A", "Vendor A" },
            new[] { "Item B", "Vendor B" },
            new[] { "Item C", "Vendor C" });

        var (success, _, batchId) = await svc.StageFromExcelAsync(wb, "test.xlsx", null, null);
        Assert.True(success);

        var batch = ctx.MrsImportBatches.Single(x => x.MrsImportBatchId == batchId);
        var actualRowCount = ctx.MrsImportRows.Count(x => x.MrsImportBatchId == batchId);

        Assert.Equal(batch.TotalRows, actualRowCount);
        Assert.Equal(3, actualRowCount);

        // هیچ ردیف Metadata یتیم (بدون MrsImportRow معتبر) وجود ندارد
        var allRowIds = ctx.MrsImportRows.Select(x => x.MrsImportRowId).ToHashSet();
        var orphanMeta = ctx.MrsImportRowMetadata.Where(m => !allRowIds.Contains(m.MrsImportRowId)).ToList();
        Assert.Empty(orphanMeta);
    }

    // ==================== Vendor-linkage fix (bonus, directly serves Test 2/3/6) ====================
    [Fact]
    public async Task CommitAsync_NewMaterialWithSupplier_SetsRealVendorId_NotJustManufacturerText()
    {
        var (svc, ctx) = Seed(nameof(CommitAsync_NewMaterialWithSupplier_SetsRealVendorId_NotJustManufacturerText));

        var headers = new[] { "DESCRIPTION PACKING LIST", "VENDOR" };
        using var wb = BuildWorkbook(headers, new[] { "Filter Element 5 micron", "Parker Hannifin" });

        var (success, _, batchId) = await svc.StageFromExcelAsync(wb, "test.xlsx", null, null);
        Assert.True(success);

        var row = ctx.MrsImportRows.Single(x => x.MrsImportBatchId == batchId);
        await svc.CommitAsync(batchId!.Value, new List<int> { row.MrsImportRowId }, null);

        var material = ctx.MaterialItems.Include(x => x.Vendor).Single(x => x.Description == "Filter Element 5 micron");
        Assert.NotNull(material.VendorId);
        Assert.Equal("Parker Hannifin", material.Vendor!.Name);
    }
}
