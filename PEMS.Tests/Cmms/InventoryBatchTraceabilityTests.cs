using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۱۴: اثبات ۱۰ رفتار مورد نیاز -
/// InventoryTransaction همچنان تنها دفتر کل است (بدون لجر دوم)، MaterialStock بدون تغییر Grain،
/// و ProductionBatchId فقط وقتی پر می‌شود که صدازننده واقعاً می‌داند.
/// </summary>
public class InventoryBatchTraceabilityTests
{
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"InventoryBatchTraceability_{dbName}");

    private static async Task<(MaterialItem material, Warehouse warehouse, ProductionWorkCenter wc)> SeedBaselineAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
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

        return (material, warehouse, wc);
    }

    // ================= #1: Production output creates a batch-linked transaction =================

    [Fact]
    public async Task ProductionOutput_PostedToInventory_CreatesBatchLinkedTransaction()
    {
        var ctx = CreateContext(nameof(ProductionOutput_PostedToInventory_CreatesBatchLinkedTransaction));
        var (material, warehouse, wc) = await SeedBaselineAsync(ctx);
        var numbering = new FakeNumberingService();
        var orderService = new ProductionOrderService(ctx, numbering);
        var materialStockService = new MaterialStockService(ctx);
        var runService = new ProductionRunService(ctx, numbering, materialStockService, orderService);

        var (_, _, orderId) = await orderService.CreateAsync(material.MaterialItemId, 100m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);
        var (_, _, runId) = await runService.CreateAsync(orderId.Value, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        var (_, _, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 30m, null, "Primary", null, null);

        var run = await runService.GetByIdAsync(runId.Value);
        var outputId = run!.Outputs.Single().ProductionOutputId;
        var postResult = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.True(postResult.Success, postResult.Error);
        var tx = ctx.InventoryTransactions.Single();
        Assert.Equal(batchId, tx.ProductionBatchId);
    }

    // ================= #2 / #9: Existing non-batch transactions remain valid =================

    [Fact]
    public async Task ReceiveAsync_WithoutBatch_ProductionBatchIdStaysNull_BehaviorUnchanged()
    {
        var ctx = CreateContext(nameof(ReceiveAsync_WithoutBatch_ProductionBatchIdStaysNull_BehaviorUnchanged));
        var (material, warehouse, _) = await SeedBaselineAsync(ctx);
        var service = new MaterialStockService(ctx);

        var (success, error) = await service.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 50m, "MRS import", null);

        Assert.True(success, error);
        var tx = ctx.InventoryTransactions.Single();
        Assert.Null(tx.ProductionBatchId);
        var stock = ctx.MaterialStocks.Single();
        Assert.Equal(50m, stock.OnHandQuantity);
    }

    // ================= #3: Batch-linked transactions can be queried by batch =================

    [Fact]
    public async Task GetBatchBalancesAsync_ReturnsOnlyBatchLinkedRows_GroupedByBatch()
    {
        var ctx = CreateContext(nameof(GetBatchBalancesAsync_ReturnsOnlyBatchLinkedRows_GroupedByBatch));
        var (material, warehouse, _) = await SeedBaselineAsync(ctx);
        var batch = new ProductionBatch { BatchNumber = "PB-001", MaterialItemId = material.MaterialItemId, ProductionOrderId = 1, ProductionRunId = 1, Quantity = 10, QualityStatus = "Released" };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var stockService = new MaterialStockService(ctx);
        await stockService.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 40m, null, null, productionBatchId: batch.ProductionBatchId);
        await stockService.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 10m, "no batch", null); // بدون بچ - نباید در خروجی ظاهر شود

        var ledgerService = new InventoryTransactionService(ctx);
        var balances = await ledgerService.GetBatchBalancesAsync(material.MaterialItemId, warehouse.WarehouseId);

        var row = Assert.Single(balances);
        Assert.Equal(batch.ProductionBatchId, row.ProductionBatchId);
        Assert.Equal(40m, row.Balance);
    }

    // ================= #4: Batch-linked transfer preserves the batch =================

    [Fact]
    public async Task TransferAsync_WithBatch_BatchIdentitySurvivesOnBothLegs()
    {
        var ctx = CreateContext(nameof(TransferAsync_WithBatch_BatchIdentitySurvivesOnBothLegs));
        var (material, warehouseA, _) = await SeedBaselineAsync(ctx);
        var warehouseB = new Warehouse { Code = "WH-2", Name = "Secondary Warehouse", IsActive = true };
        ctx.Warehouses.Add(warehouseB);
        await ctx.SaveChangesAsync();

        var batch = new ProductionBatch { BatchNumber = "PB-002", MaterialItemId = material.MaterialItemId, ProductionOrderId = 1, ProductionRunId = 1, Quantity = 10, QualityStatus = "Released" };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var service = new MaterialStockService(ctx);
        await service.ReceiveAsync(material.MaterialItemId, warehouseA.WarehouseId, null, 20m, null, null, productionBatchId: batch.ProductionBatchId);

        var transferResult = await service.TransferAsync(
            material.MaterialItemId, warehouseA.WarehouseId, null, warehouseB.WarehouseId, null, 20m, null, null,
            productionBatchId: batch.ProductionBatchId);

        Assert.True(transferResult.Success, transferResult.Error);

        var transferTxs = ctx.InventoryTransactions.Where(x => x.TransactionType == "TransferOut" || x.TransactionType == "TransferIn").ToList();
        Assert.Equal(2, transferTxs.Count);
        Assert.All(transferTxs, tx => Assert.Equal(batch.ProductionBatchId, tx.ProductionBatchId));
    }

    // ================= #5: Batch-linked issue (consumption) preserves the batch reference =================

    [Fact]
    public async Task AdjustAsync_WithBatch_ConsumptionPreservesBatchReference()
    {
        var ctx = CreateContext(nameof(AdjustAsync_WithBatch_ConsumptionPreservesBatchReference));
        var (material, warehouse, _) = await SeedBaselineAsync(ctx);
        var batch = new ProductionBatch { BatchNumber = "PB-003", MaterialItemId = material.MaterialItemId, ProductionOrderId = 1, ProductionRunId = 1, Quantity = 10, QualityStatus = "Released" };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var service = new MaterialStockService(ctx);
        await service.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 20m, null, null, productionBatchId: batch.ProductionBatchId);

        var adjustResult = await service.AdjustAsync(
            material.MaterialItemId, warehouse.WarehouseId, null, 12m, "consumed 8", null,
            productionBatchId: batch.ProductionBatchId);

        Assert.True(adjustResult.Success, adjustResult.Error);
        var adjustmentTx = ctx.InventoryTransactions.Single(x => x.TransactionType == "Adjustment");
        Assert.Equal(batch.ProductionBatchId, adjustmentTx.ProductionBatchId);
    }

    // ================= #6: Batch balances reconcile with the transaction ledger =================

    [Fact]
    public async Task BatchBalance_ReconcilesWithSumOfLedgerTransactions()
    {
        var ctx = CreateContext(nameof(BatchBalance_ReconcilesWithSumOfLedgerTransactions));
        var (material, warehouse, _) = await SeedBaselineAsync(ctx);
        var batch = new ProductionBatch { BatchNumber = "PB-004", MaterialItemId = material.MaterialItemId, ProductionOrderId = 1, ProductionRunId = 1, Quantity = 10, QualityStatus = "Released" };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var stockService = new MaterialStockService(ctx);
        await stockService.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 50m, null, null, productionBatchId: batch.ProductionBatchId);
        await stockService.AdjustAsync(material.MaterialItemId, warehouse.WarehouseId, null, 35m, "consumed 15", null, productionBatchId: batch.ProductionBatchId);

        var ledgerService = new InventoryTransactionService(ctx);
        var balances = await ledgerService.GetBatchBalancesAsync(material.MaterialItemId, warehouse.WarehouseId, batch.ProductionBatchId);

        // ۵۰ (Receipt) + (-۱۵) (Adjustment Delta) = ۳۵ - دقیقاً هم‌راستا با MaterialStock.OnHandQuantity
        var expectedBalance = ctx.MaterialStocks.Single().OnHandQuantity;
        Assert.Equal(expectedBalance, balances.Single().Balance);
        Assert.Equal(35m, balances.Single().Balance);
    }

    // ================= #7: Quality status can be resolved from ProductionBatch =================

    [Fact]
    public async Task BatchBalance_ResolvesQualityStatus_FromProductionBatch_NotDuplicated()
    {
        var ctx = CreateContext(nameof(BatchBalance_ResolvesQualityStatus_FromProductionBatch_NotDuplicated));
        var (material, warehouse, _) = await SeedBaselineAsync(ctx);
        var batch = new ProductionBatch { BatchNumber = "PB-005", MaterialItemId = material.MaterialItemId, ProductionOrderId = 1, ProductionRunId = 1, Quantity = 10, QualityStatus = "AwaitingQC" };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var stockService = new MaterialStockService(ctx);
        await stockService.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 10m, null, null, productionBatchId: batch.ProductionBatchId);

        var ledgerService = new InventoryTransactionService(ctx);
        var before = (await ledgerService.GetBatchBalancesAsync(productionBatchId: batch.ProductionBatchId)).Single();
        Assert.Equal("AwaitingQC", before.QualityStatus);

        // طبق بخش ۱۳ سند فاز: QualityStatus هرگز در سمت موجودی کپی نمی‌شود - تغییر آن روی
        // ProductionBatch باید بلافاصله (بدون هیچ همگام‌سازی دستی) در Query بعدی منعکس شود.
        batch.QualityStatus = "Released";
        await ctx.SaveChangesAsync();

        var after = (await ledgerService.GetBatchBalancesAsync(productionBatchId: batch.ProductionBatchId)).Single();
        Assert.Equal("Released", after.QualityStatus);
    }

    // ================= #8: No duplicate inventory posting occurs =================

    [Fact]
    public async Task PostOutputToInventory_Twice_StillRejected_WithBatchLink()
    {
        var ctx = CreateContext(nameof(PostOutputToInventory_Twice_StillRejected_WithBatchLink));
        var (material, warehouse, wc) = await SeedBaselineAsync(ctx);
        var numbering = new FakeNumberingService();
        var orderService = new ProductionOrderService(ctx, numbering);
        var runService = new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), orderService);

        var (_, _, orderId) = await orderService.CreateAsync(material.MaterialItemId, 100m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);
        var (_, _, runId) = await runService.CreateAsync(orderId.Value, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 20m, null, "Primary", null, null);

        var run = await runService.GetByIdAsync(runId.Value);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var first = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);
        var second = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.True(first.Success, first.Error);
        Assert.False(second.Success);
        Assert.Single(ctx.InventoryTransactions);
    }

    // ================= #10: Historical transactions are not fabricated with batch IDs =================

    [Fact]
    public async Task GetBatchBalancesAsync_NeverFabricatesBatchForNonBatchTransactions()
    {
        var ctx = CreateContext(nameof(GetBatchBalancesAsync_NeverFabricatesBatchForNonBatchTransactions));
        var (material, warehouse, _) = await SeedBaselineAsync(ctx);
        var stockService = new MaterialStockService(ctx);

        // سه تراکنش قدیمی/دستی بدون هیچ ارتباطی با بچ
        await stockService.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 10m, "manual receipt 1", null);
        await stockService.ReceiveAsync(material.MaterialItemId, warehouse.WarehouseId, null, 5m, "manual receipt 2", null);
        await stockService.ReturnAsync(material.MaterialItemId, warehouse.WarehouseId, null, 2m, "manual return", null);

        var ledgerService = new InventoryTransactionService(ctx);
        var balances = await ledgerService.GetBatchBalancesAsync();

        Assert.Empty(balances);
        Assert.All(ctx.InventoryTransactions, tx => Assert.Null(tx.ProductionBatchId));
    }
}
