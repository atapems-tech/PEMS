using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱: تست‌های دامنه‌ی
/// ProductionOrder - گذارهای مجاز/غیرمجاز، اعتبارسنجی مقدار، و Hold/Resume با PreHoldStatus.
/// </summary>
public class ProductionOrderServiceTests
{
    // NumberingService واقعی از T-SQL خام استفاده می‌کند (InMemory را پشتیبانی نمی‌کند) - طبق
    // همون الگوی Phase6ExecutionTests.FakeNumberingService.
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"ProductionOrder_{dbName}");

    private static async Task<(MaterialItem material, ProductionWorkCenter workCenter)> SeedBaselineAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var fl = new FunctionalLocation { Code = "FL-1", Name = "Area 100", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);

        var workCenter = new ProductionWorkCenter { Code = "WC-1", Name = "Line 1", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.ProductionWorkCenters.Add(workCenter);

        await ctx.SaveChangesAsync();
        return (material, workCenter);
    }

    private static ProductionOrderService CreateService(PEMS.Persistence.Context.PEMSDbContext ctx) =>
        new(ctx, new FakeNumberingService());

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesDraftOrder()
    {
        var ctx = CreateContext(nameof(CreateAsync_ValidInput_CreatesDraftOrder));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (success, error, orderId) = await service.CreateAsync(
            material.MaterialItemId, 100m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);

        Assert.True(success, error);
        Assert.NotNull(orderId);
        var order = await service.GetByIdAsync(orderId!.Value);
        Assert.Equal("Draft", order!.Status);
    }

    [Fact]
    public async Task CreateAsync_ZeroOrNegativeQuantity_Fails()
    {
        var ctx = CreateContext(nameof(CreateAsync_ZeroOrNegativeQuantity_Fails));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (success, error, orderId) = await service.CreateAsync(
            material.MaterialItemId, 0m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);

        Assert.False(success);
        Assert.Null(orderId);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_InvalidMaterial_Fails()
    {
        var ctx = CreateContext(nameof(CreateAsync_InvalidMaterial_Fails));
        var (_, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (success, _, _) = await service.CreateAsync(99999, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);

        Assert.False(success);
    }

    [Fact]
    public async Task FullLifecycle_DraftToClosed_Succeeds()
    {
        var ctx = CreateContext(nameof(FullLifecycle_DraftToClosed_Succeeds));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var numbering = new FakeNumberingService();
        var service = new ProductionOrderService(ctx, numbering);
        var runService = new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), service);

        var (_, _, orderId) = await service.CreateAsync(material.MaterialItemId, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);

        Assert.True((await service.PlanAsync(orderId!.Value, null)).Success);
        Assert.True((await service.ReleaseAsync(orderId.Value, null)).Success);

        // طبق طراحی عمدی: Complete فقط بعد از این‌که سفارش واقعاً وارد InProgress شده باشد مجاز
        // است (یعنی حداقل یک Run واقعاً Start شده) - نه فقط چون هیچ Run ناتمامی نیست.
        var (_, _, runId) = await runService.CreateAsync(orderId.Value, null, null, null, null, null, null);
        Assert.True((await runService.StartAsync(runId!.Value, null)).Success);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 10m, null, "Primary", null, null);
        Assert.True((await runService.CompleteAsync(runId.Value, null)).Success);

        Assert.True((await service.CompleteAsync(orderId.Value, null)).Success);
        Assert.True((await service.CloseAsync(orderId.Value, null)).Success);

        var order = await service.GetByIdAsync(orderId.Value);
        Assert.Equal("Closed", order!.Status);
    }

    [Fact]
    public async Task Release_FromDraft_IsRejected_IllegalTransition()
    {
        var ctx = CreateContext(nameof(Release_FromDraft_IsRejected_IllegalTransition));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (_, _, orderId) = await service.CreateAsync(material.MaterialItemId, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);

        var (success, error) = await service.ReleaseAsync(orderId!.Value, null);

        Assert.False(success);
        Assert.Contains("مجاز نیست", error);
    }

    [Fact]
    public async Task Close_FromCompleted_IsRejected_MustCompleteFirst()
    {
        var ctx = CreateContext(nameof(Close_FromCompleted_IsRejected_MustCompleteFirst));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (_, _, orderId) = await service.CreateAsync(material.MaterialItemId, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await service.PlanAsync(orderId!.Value, null);
        await service.ReleaseAsync(orderId.Value, null);

        var (success, error) = await service.CloseAsync(orderId.Value, null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Hold_ThenResume_ReturnsToExactPreviousStatus_NotAlwaysInProgress()
    {
        var ctx = CreateContext(nameof(Hold_ThenResume_ReturnsToExactPreviousStatus_NotAlwaysInProgress));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (_, _, orderId) = await service.CreateAsync(material.MaterialItemId, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await service.PlanAsync(orderId!.Value, null);
        await service.ReleaseAsync(orderId.Value, null);

        // در وضعیت Released (نه InProgress) به تعلیق درمی‌آید
        var holdResult = await service.HoldAsync(orderId.Value, "دلیل تست", null);
        Assert.True(holdResult.Success, holdResult.Error);

        var held = await service.GetByIdAsync(orderId.Value);
        Assert.Equal("OnHold", held!.Status);

        var resumeResult = await service.ResumeAsync(orderId.Value, null);
        Assert.True(resumeResult.Success, resumeResult.Error);

        var resumed = await service.GetByIdAsync(orderId.Value);
        // طبق کامنت ProductionOrder.PreHoldStatus: باید دقیقاً به Released برگردد، نه InProgress
        Assert.Equal("Released", resumed!.Status);
    }

    [Fact]
    public async Task Cancel_WithoutReason_Fails()
    {
        var ctx = CreateContext(nameof(Cancel_WithoutReason_Fails));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = CreateService(ctx);

        var (_, _, orderId) = await service.CreateAsync(material.MaterialItemId, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);

        var (success, error) = await service.CancelAsync(orderId!.Value, "", null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Cancel_FromClosedOrCompleted_IsRejected_IllegalTransition()
    {
        var ctx = CreateContext(nameof(Cancel_FromClosedOrCompleted_IsRejected_IllegalTransition));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var numbering = new FakeNumberingService();
        var service = new ProductionOrderService(ctx, numbering);
        var runService = new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), service);

        var (_, _, orderId) = await service.CreateAsync(material.MaterialItemId, 10m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await service.PlanAsync(orderId!.Value, null);
        await service.ReleaseAsync(orderId.Value, null);

        var (_, _, runId) = await runService.CreateAsync(orderId.Value, null, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);
        await runService.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 10m, null, "Primary", null, null);
        await runService.CompleteAsync(runId.Value, null);

        var completeResult = await service.CompleteAsync(orderId.Value, null);
        Assert.True(completeResult.Success, completeResult.Error);

        var (success, _) = await service.CancelAsync(orderId.Value, "دیر شده", null);

        Assert.False(success);
    }
}
