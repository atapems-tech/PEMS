using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations Phase 2C — Production Safety Correction»: تست‌های اختصاصی گیت کیفیت
/// روی ProductionRunService.PostOutputToInventoryAsync - قبل از این فاز، این متد فقط
/// InventoryTransactionId.HasValue (محافظت از Post تکراری) را بررسی می‌کرد و QualityStatus بچ
/// را اصلاً نمی‌خواند؛ یعنی یک بچ Blocked/Rejected می‌توانست عیناً مثل یک بچ Released وارد
/// موجودیِ در دسترس شود. این فایل هر پنج وضعیت واقعی QualityStatus را جدا و صریح تست می‌کند -
/// نه فقط دو حالت منفی (Blocked/Rejected) که در توضیح اولیه‌ی نقص آمده بود.
/// </summary>
public class ProductionInventoryQualityGateTests
{
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"ProductionInventoryQualityGate_{dbName}");

    private static async Task<(MaterialItem material, Warehouse warehouse, int runId, ProductionRunService runService, int batchId)> SeedRunWithOutputAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, string dbNameSuffix)
    {
        var fl = new FunctionalLocation { Code = "FL-1", Name = "Area 100", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);
        var warehouse = new Warehouse { Code = "WH-1", Name = "Main Warehouse", IsActive = true };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var wc = new ProductionWorkCenter { Code = "WC-1", Name = "Line 1", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.ProductionWorkCenters.Add(wc);
        await ctx.SaveChangesAsync();

        var numbering = new FakeNumberingService();
        var orderService = new ProductionOrderService(ctx, numbering);
        var runService = new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), orderService);

        var (_, _, orderId) = await orderService.CreateAsync(material.MaterialItemId, 100m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);
        var (_, _, runId) = await runService.CreateAsync(orderId.Value, wc.ProductionWorkCenterId, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        var (_, _, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 25m, null, "Primary", null, null);

        return (material, warehouse, runId.Value, runService, batchId!.Value);
    }

    // ================= Positive case: Released batch =================

    [Fact]
    public async Task PostOutputToInventoryAsync_ReleasedBatch_Succeeds()
    {
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_ReleasedBatch_Succeeds));
        var (_, warehouse, runId, runService, batchId) = await SeedRunWithOutputAsync(ctx, "released");
        await runService.SubmitBatchForQcAsync(batchId, null);
        await runService.ReleaseBatchAsync(batchId, null);

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var result = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.True(result.Success, result.Error);
        Assert.Single(ctx.InventoryTransactions);
    }

    // ================= Negative cases: every non-Released status, tested individually =================

    [Fact]
    public async Task PostOutputToInventoryAsync_ProducedBatch_IsRejected_OriginalDefectScenario()
    {
        // این دقیقاً همون سناریوی نقص اصلی است: بچ تازه‌ساخته‌شده هنوز حتی برای QC ارسال نشده
        // (وضعیت پیش‌فرض RecordOutputAsync = "Produced") - قبل از این اصلاح، Post موفق می‌شد.
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_ProducedBatch_IsRejected_OriginalDefectScenario));
        var (_, warehouse, runId, runService, _) = await SeedRunWithOutputAsync(ctx, "produced");

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var result = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.False(result.Success);
        Assert.Contains("Released", result.Error);
    }

    [Fact]
    public async Task PostOutputToInventoryAsync_AwaitingQcBatch_IsRejected()
    {
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_AwaitingQcBatch_IsRejected));
        var (_, warehouse, runId, runService, batchId) = await SeedRunWithOutputAsync(ctx, "awaitingqc");
        await runService.SubmitBatchForQcAsync(batchId, null);

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var result = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task PostOutputToInventoryAsync_BlockedBatch_IsRejected()
    {
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_BlockedBatch_IsRejected));
        var (_, warehouse, runId, runService, batchId) = await SeedRunWithOutputAsync(ctx, "blocked");
        await runService.SubmitBatchForQcAsync(batchId, null);
        await runService.BlockBatchAsync(batchId, "شک به آلودگی", null);

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var result = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.False(result.Success);
        Assert.Empty(ctx.InventoryTransactions);
    }

    [Fact]
    public async Task PostOutputToInventoryAsync_RejectedBatch_IsRejected()
    {
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_RejectedBatch_IsRejected));
        var (_, warehouse, runId, runService, batchId) = await SeedRunWithOutputAsync(ctx, "rejected");
        await runService.SubmitBatchForQcAsync(batchId, null);
        await runService.RejectBatchAsync(batchId, "خارج از مشخصات", null);

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var result = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.False(result.Success);
        Assert.Empty(ctx.InventoryTransactions);
    }

    // ================= Side-effect isolation: a rejected gate must leave zero footprint =================

    [Fact]
    public async Task PostOutputToInventoryAsync_GateFailure_CreatesNoInventoryTransaction()
    {
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_GateFailure_CreatesNoInventoryTransaction));
        var (_, warehouse, runId, runService, _) = await SeedRunWithOutputAsync(ctx, "notx");

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.Empty(ctx.InventoryTransactions);
    }

    [Fact]
    public async Task PostOutputToInventoryAsync_GateFailure_DoesNotAlterMaterialStock()
    {
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_GateFailure_DoesNotAlterMaterialStock));
        var (_, warehouse, runId, runService, _) = await SeedRunWithOutputAsync(ctx, "nostock");

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.Empty(ctx.MaterialStocks);
    }

    [Fact]
    public async Task PostOutputToInventoryAsync_GateFailure_OutputRemainsNotPosted_CanBeRetriedAfterRelease()
    {
        // طبق بخش ۴.۴ سند فاز («Whether the operation remains idempotent»): یک تلاش رد‌شده
        // رکورد را قفل/خراب نمی‌کند - بعد از Release شدن واقعی، همان ردیف هنوز قابل Post است.
        var ctx = CreateContext(nameof(PostOutputToInventoryAsync_GateFailure_OutputRemainsNotPosted_CanBeRetriedAfterRelease));
        var (_, warehouse, runId, runService, batchId) = await SeedRunWithOutputAsync(ctx, "retry");

        var run = await runService.GetByIdAsync(runId);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var firstAttempt = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);
        Assert.False(firstAttempt.Success);

        await runService.SubmitBatchForQcAsync(batchId, null);
        await runService.ReleaseBatchAsync(batchId, null);

        var secondAttempt = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);
        Assert.True(secondAttempt.Success, secondAttempt.Error);
        Assert.Single(ctx.InventoryTransactions);
    }
}
