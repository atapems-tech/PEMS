using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱: تست‌های دامنه‌ی
/// ProductionRun/Input/Output/Loss/Batch - زنجیره‌ی کامل Order→Run→Input/Output→Batch،
/// یکپارچگی با Equipment/FunctionalLocation (از طریق WorkCenter)، و ارسال اختیاری به
/// InventoryTransaction موجود (نه یک لجر دوم).
/// </summary>
public class ProductionRunServiceTests
{
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"ProductionRun_{dbName}");

    private static async Task<(MaterialItem material, ProductionWorkCenter workCenter, Equipment equipment, FunctionalLocation fl)> SeedBaselineAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var fl = new FunctionalLocation { Code = "FL-1", Name = "Area 100", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var equipment = new Equipment { TagNumber = "P-101", Name = "Reactor Pump", EquipmentType = "Pump", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.Equipments.Add(equipment);

        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);

        var workCenter = new ProductionWorkCenter { Code = "WC-1", Name = "Line 1", FunctionalLocationId = fl.FunctionalLocationId, EquipmentId = equipment.EquipmentId, IsActive = true };
        ctx.ProductionWorkCenters.Add(workCenter);

        await ctx.SaveChangesAsync();
        return (material, workCenter, equipment, fl);
    }

    private static async Task<int> CreateReleasedOrderAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, ProductionOrderService orderService, int materialItemId, int workCenterId)
    {
        var (success, error, orderId) = await orderService.CreateAsync(materialItemId, 100m, null, workCenterId, null, null, null, null, null, null);
        Assert.True(success, error);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);
        return orderId.Value;
    }

    private static (ProductionOrderService orderService, ProductionRunService runService) CreateServices(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var numbering = new FakeNumberingService();
        var orderService = new ProductionOrderService(ctx, numbering);
        var materialStockService = new MaterialStockService(ctx);
        var runService = new ProductionRunService(ctx, numbering, materialStockService, orderService);
        return (orderService, runService);
    }

    [Fact]
    public async Task StartRun_AdvancesParentOrderToInProgress()
    {
        var ctx = CreateContext(nameof(StartRun_AdvancesParentOrderToInProgress));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);

        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        var startResult = await runService.StartAsync(runId!.Value, null);

        Assert.True(startResult.Success, startResult.Error);

        var order = await orderService.GetByIdAsync(orderId);
        Assert.Equal("InProgress", order!.Status);
    }

    [Fact]
    public async Task Complete_WithoutAnyOutput_IsRejected_WithActionableMessage()
    {
        var ctx = CreateContext(nameof(Complete_WithoutAnyOutput_IsRejected_WithActionableMessage));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        var (success, error) = await runService.CompleteAsync(runId.Value, null);

        Assert.False(success);
        Assert.Contains("خروجی", error);
    }

    [Fact]
    public async Task RecordOutput_FirstTime_CreatesNewBatch_WithProducedStatus()
    {
        var ctx = CreateContext(nameof(RecordOutput_FirstTime_CreatesNewBatch_WithProducedStatus));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        var (success, error, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 25m, null, "Primary", null, null);

        Assert.True(success, error);
        var batch = await runService.GetBatchByIdAsync(batchId!.Value);
        Assert.Equal(25m, batch!.Quantity);
        Assert.Equal("Produced", batch.QualityStatus);
    }

    [Fact]
    public async Task RecordOutput_SameBatchTwice_AccumulatesQuantity_DoesNotOverwrite()
    {
        var ctx = CreateContext(nameof(RecordOutput_SameBatchTwice_AccumulatesQuantity_DoesNotOverwrite));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        var (_, _, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 10m, null, "Primary", null, null);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, batchId, 15m, null, "Primary", null, null);

        var batch = await runService.GetBatchByIdAsync(batchId!.Value);
        Assert.Equal(25m, batch!.Quantity);
    }

    [Fact]
    public async Task RecordInputOutputLoss_WhenRunNotInProgress_AreRejected()
    {
        var ctx = CreateContext(nameof(RecordInputOutputLoss_WhenRunNotInProgress_AreRejected));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        // عمداً Start نمی‌شود - هنوز Planned است

        var inputResult = await runService.RecordInputAsync(runId!.Value, material.MaterialItemId, 5m, null, null, null, null);
        var outputResult = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 5m, null, "Primary", null, null);
        var lossResult = await runService.RecordLossAsync(runId.Value, 1m, null, "Waste", null, null);

        Assert.False(inputResult.Success);
        Assert.False(outputResult.Success);
        Assert.False(lossResult.Success);
    }

    [Fact]
    public async Task RecordLoss_WithUncontrolledFreeTextCategory_IsRejected()
    {
        var ctx = CreateContext(nameof(RecordLoss_WithUncontrolledFreeTextCategory_IsRejected));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        var (success, error) = await runService.RecordLossAsync(runId.Value, 3m, null, "این یک دسته‌بندی دلخواه است", null, null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Reconciliation_SameUom_ComputesDifference_WithoutForcingBalance()
    {
        var ctx = CreateContext(nameof(Reconciliation_SameUom_ComputesDifference_WithoutForcingBalance));
        var uom = new MaterialUOM { Code = "KG", Name = "Kilogram", IsActive = true };
        ctx.MaterialUOMs.Add(uom);
        await ctx.SaveChangesAsync();

        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        await runService.RecordInputAsync(runId.Value, material.MaterialItemId, 100m, uom.MaterialUOMId, null, null, null);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 90m, uom.MaterialUOMId, "Primary", null, null);
        await runService.RecordLossAsync(runId.Value, 5m, uom.MaterialUOMId, "ProcessLoss", null, null);

        var run = await runService.GetByIdAsync(runId.Value);

        Assert.True(run!.Reconciliation.IsComparable);
        // ۱۰۰ ورودی - ۹۰ خروجی - ۵ افت = ۵ اختلاف واقعی، عمداً صاف نمی‌شود
        Assert.Equal(5m, run.Reconciliation.Difference);
    }

    [Fact]
    public async Task Reconciliation_MixedUoms_IsReportedAsNotComparable_NotSilentlyEqualized()
    {
        var ctx = CreateContext(nameof(Reconciliation_MixedUoms_IsReportedAsNotComparable_NotSilentlyEqualized));
        var kg = new MaterialUOM { Code = "KG", Name = "Kilogram", IsActive = true };
        var lt = new MaterialUOM { Code = "LT", Name = "Liter", IsActive = true };
        ctx.MaterialUOMs.AddRange(kg, lt);
        await ctx.SaveChangesAsync();

        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        await runService.RecordInputAsync(runId.Value, material.MaterialItemId, 100m, kg.MaterialUOMId, null, null, null);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 90m, lt.MaterialUOMId, "Primary", null, null);

        var run = await runService.GetByIdAsync(runId.Value);

        Assert.False(run!.Reconciliation.IsComparable);
        Assert.Null(run.Reconciliation.Difference);
    }

    // ================= Quality Gate =================

    [Fact]
    public async Task BatchQuality_ProducedToReleased_MustPassThroughAwaitingQC()
    {
        var ctx = CreateContext(nameof(BatchQuality_ProducedToReleased_MustPassThroughAwaitingQC));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        var (_, _, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 10m, null, "Primary", null, null);

        // نمی‌توان مستقیم از Produced به Released پرید
        var directRelease = await runService.ReleaseBatchAsync(batchId!.Value, null);
        Assert.False(directRelease.Success);

        var submit = await runService.SubmitBatchForQcAsync(batchId.Value, null);
        Assert.True(submit.Success, submit.Error);

        var release = await runService.ReleaseBatchAsync(batchId.Value, null);
        Assert.True(release.Success, release.Error);

        var batch = await runService.GetBatchByIdAsync(batchId.Value);
        Assert.Equal("Released", batch!.QualityStatus);
    }

    [Fact]
    public async Task BatchQuality_Block_RequiresReason_ThenCanReturnToAwaitingQC()
    {
        var ctx = CreateContext(nameof(BatchQuality_Block_RequiresReason_ThenCanReturnToAwaitingQC));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        var (_, _, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 10m, null, "Primary", null, null);
        await runService.SubmitBatchForQcAsync(batchId!.Value, null);

        var blockWithoutReason = await runService.BlockBatchAsync(batchId.Value, "", null);
        Assert.False(blockWithoutReason.Success);

        var block = await runService.BlockBatchAsync(batchId.Value, "شک به آلودگی", null);
        Assert.True(block.Success, block.Error);

        var unblock = await runService.UnblockBatchAsync(batchId.Value, null);
        Assert.True(unblock.Success, unblock.Error);

        var batch = await runService.GetBatchByIdAsync(batchId.Value);
        Assert.Equal("AwaitingQC", batch!.QualityStatus);
    }

    // ================= Inventory Integration (reuse of existing InventoryTransaction) =================

    [Fact]
    public async Task PostOutputToInventory_ReusesExistingInventoryTransaction_AndIsNotAutomatic()
    {
        var ctx = CreateContext(nameof(PostOutputToInventory_ReusesExistingInventoryTransaction_AndIsNotAutomatic));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var warehouse = new Warehouse { Code = "WH-1", Name = "Main Warehouse", IsActive = true };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        var (_, _, batchId) = await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 40m, null, "Primary", null, null);

        var runBeforePost = await runService.GetByIdAsync(runId.Value);
        // طبق تصمیم معماری AD-Phase1-01: ثبت خروجی به‌خودی‌خود موجودی را تغییر نمی‌دهد
        Assert.False(runBeforePost!.Outputs.Single().PostedToInventory);
        Assert.Empty(ctx.InventoryTransactions);

        var outputId = runBeforePost.Outputs.Single().ProductionOutputId;
        var postResult = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);

        Assert.True(postResult.Success, postResult.Error);
        Assert.Single(ctx.InventoryTransactions);
        var tx = ctx.InventoryTransactions.Single();
        Assert.Equal("Receipt", tx.TransactionType);
        Assert.Equal("ProductionOutput", tx.ReferenceType);
        Assert.Equal(outputId, tx.ReferenceId);
        Assert.Equal(40m, tx.Quantity);

        var runAfterPost = await runService.GetByIdAsync(runId.Value);
        Assert.True(runAfterPost!.Outputs.Single().PostedToInventory);
    }

    [Fact]
    public async Task PostOutputToInventory_Twice_IsRejected_NoDuplicatePosting()
    {
        var ctx = CreateContext(nameof(PostOutputToInventory_Twice_IsRejected_NoDuplicatePosting));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var warehouse = new Warehouse { Code = "WH-1", Name = "Main Warehouse", IsActive = true };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 20m, null, "Primary", null, null);

        var run = await runService.GetByIdAsync(runId.Value);
        var outputId = run!.Outputs.Single().ProductionOutputId;

        var first = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);
        Assert.True(first.Success, first.Error);

        var second = await runService.PostOutputToInventoryAsync(outputId, warehouse.WarehouseId, null, null);
        Assert.False(second.Success);
        // فقط یک تراکنش، نه دو
        Assert.Single(ctx.InventoryTransactions);
    }

    [Fact]
    public async Task PostInputToInventory_InsufficientStock_IsRejected_NotNegativeBalance()
    {
        var ctx = CreateContext(nameof(PostInputToInventory_InsufficientStock_IsRejected_NotNegativeBalance));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var warehouse = new Warehouse { Code = "WH-1", Name = "Main Warehouse", IsActive = true };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();

        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        await runService.RecordInputAsync(runId.Value, material.MaterialItemId, 50m, null, null, null, null);

        var run = await runService.GetByIdAsync(runId.Value);
        var inputId = run!.Inputs.Single().ProductionInputId;

        // هیچ موجودی اولیه‌ای برای این متریال در این انبار وجود ندارد
        var postResult = await runService.PostInputToInventoryAsync(inputId, warehouse.WarehouseId, null, null);

        Assert.False(postResult.Success);
        Assert.Empty(ctx.InventoryTransactions);
    }

    // ================= Equipment / Functional Location integration =================

    [Fact]
    public async Task ProductionRun_ReachesFunctionalLocation_ThroughExistingWorkCenterAndEquipment_NoDuplicateEntities()
    {
        var ctx = CreateContext(nameof(ProductionRun_ReachesFunctionalLocation_ThroughExistingWorkCenterAndEquipment_NoDuplicateEntities));
        var (material, wc, equipment, fl) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, wc.ProductionWorkCenterId, equipment.EquipmentId, null, null, null, null);

        var run = await runService.GetByIdAsync(runId!.Value);

        Assert.Equal(equipment.TagNumber, run!.EquipmentTagNumber);
        Assert.Equal(wc.Code, run.WorkCenterCode);
        // اثبات این‌که هیچ Equipment/FunctionalLocation موازی ساخته نشده - فقط همان‌هایی که
        // در SeedBaselineAsync ساخته شدند وجود دارند.
        Assert.Single(ctx.Equipments);
        Assert.Single(ctx.FunctionalLocations);
    }

    // ================= Phase 2A بخش ۲۳: پوشش رگرسیون رفتار فعلیِ Rework (بدون پیاده‌سازی خودکار) =================

    [Fact]
    public async Task RecordLoss_RworkCategory_IsRecordedAsPureLoss_NoAutomaticReEntryIntoProduction()
    {
        // طبق «PEMS Operations Phase 2A» بخش ۲۳: عمداً رابطه‌ی SourceProductionLossId پیاده
        // نشده - این تست فقط رفتار فعلی را مستند/محافظت می‌کند: یک افت با دسته‌بندی Rework
        // دقیقاً مثل هر افت دیگری ثبت می‌شود و هیچ تلاش خودکاری برای بازگرداندن آن به تولید
        // (ساخت ProductionOutput جدید) وجود ندارد.
        var ctx = CreateContext(nameof(RecordLoss_RworkCategory_IsRecordedAsPureLoss_NoAutomaticReEntryIntoProduction));
        var (material, wc, _, _) = await SeedBaselineAsync(ctx);
        var (orderService, runService) = CreateServices(ctx);
        var orderId = await CreateReleasedOrderAsync(ctx, orderService, material.MaterialItemId, wc.ProductionWorkCenterId);
        var (_, _, runId) = await runService.CreateAsync(orderId, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        var lossResult = await runService.RecordLossAsync(runId.Value, 8m, null, "Rework", "قابل بازیابی - هنوز پیاده‌سازی نشده", null);

        Assert.True(lossResult.Success, lossResult.Error);
        Assert.Empty(ctx.ProductionOutputs); // هیچ خروجی خودکاری از این افت ساخته نشد
        var loss = ctx.ProductionLosses.Single();
        Assert.Equal("Rework", loss.LossCategory);
    }
}
