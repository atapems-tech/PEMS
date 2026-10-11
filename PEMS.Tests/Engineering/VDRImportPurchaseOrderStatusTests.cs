using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// Procurement Lifecycle - Work Package B: مسیر Import (VDRImportService) مستقل از ایجاد دستی PO
/// باید وضعیت قدیمی Issued را حفظ کند، حتی اگر PO دستی جدید اکنون Draft است. روی دیتابیس ایزوله‌ی
/// SQL Server (migrate کامل)، نه PEMSDB مشترک، اجرا می‌شود.
/// </summary>
public class VDRImportPurchaseOrderStatusTests : IAsyncLifetime
{
    private readonly string _dbName = "PEMS_TEST_VDRPO_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task InitializeAsync()
    {
        var csb = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName };
        _connection = csb.ConnectionString;
        await using var ctx = NewCtx();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(_connection) { InitialCatalog = "master" }.ConnectionString;
        await using var c = new SqlConnection(master);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    private PEMSDbContext NewCtx() => new(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection).Options);

    [Fact]
    public async Task ImportedPo_IsCreatedAsLegacyIssued_WhileManualPoIsDraft()
    {
        await using var ctx = NewCtx();
        var project = new Project { ProjectName = "VDR PO Test", ProjectCode = ("VP" + Guid.NewGuid().ToString("N"))[..8] };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        var batch = new VDRImportBatch { FileName = "test.xlsx", ProjectId = project.ProjectId, Status = "Staged", RowsTotal = 1 };
        ctx.VDRImportBatches.Add(batch);
        await ctx.SaveChangesAsync();
        var payload = new VDRImportRowPayload
        {
            SequenceNo = 1, VendorName = "Import Vendor", DocumentTypeCode = "DS",
            DocumentNumber = "DOC-PO-001", DocumentDescription = "Desc", PoNumber = "PO-IMPORTED-1"
        };
        ctx.VDRImportRows.Add(new VDRImportRow
        {
            VDRImportBatchId = batch.VDRImportBatchId, RowNumber = 1, DocumentNumber = "DOC-PO-001",
            VendorNameRaw = "Import Vendor", PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ValidationStatus = "Valid", IncludeInCommit = true
        });
        await ctx.SaveChangesAsync();

        var (created, _, skipped) = await new VDRImportService(ctx, NullLogger<VDRImportService>.Instance).CommitBatchAsync(batch.VDRImportBatchId);
        Assert.Equal(1, created);
        Assert.Equal(0, skipped);

        var imported = await ctx.PurchaseOrders.AsNoTracking().SingleAsync(x => x.PoNumber == "PO-IMPORTED-1");
        Assert.Equal("Issued", imported.Status);

        // ایجاد دستی در همان دیتابیس، مستقل از Import: Draft
        var vendorId = imported.VendorId;
        var manualId = await new PurchaseOrderService(ctx).CreateAsync("PO-MANUAL-1", project.ProjectId, vendorId, null, null, 5m);
        Assert.Equal("Draft", (await ctx.PurchaseOrders.AsNoTracking().SingleAsync(x => x.PoId == manualId)).Status);
        Assert.Equal("Issued", (await ctx.PurchaseOrders.AsNoTracking().SingleAsync(x => x.PoNumber == "PO-IMPORTED-1")).Status);
    }
}
