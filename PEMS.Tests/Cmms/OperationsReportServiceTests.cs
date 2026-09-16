using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۲۱-۲۲: فقط معیارهای قطعی -
/// Gross/Net عمداً غایب است (اثبات ساختاری). همیشه از ProductionOutput/ProductionLoss/
/// ProductionBatch/ProductionTarget می‌خواند - هرگز یک ذخیره‌ی دوم.
/// </summary>
public class OperationsReportServiceTests
{
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"OperationsReport_{dbName}");

    private static async Task<(MaterialItem material, ProductionWorkCenter wc, ProductionRunService runService, ProductionOrderService orderService, int runId)> SeedRunningRunAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var fl = new FunctionalLocation { Code = "FL-1", Name = "Area 100", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);
        await ctx.SaveChangesAsync();

        var wc = new ProductionWorkCenter { Code = "WC-1", Name = "Line 1", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.ProductionWorkCenters.Add(wc);
        await ctx.SaveChangesAsync();

        var numbering = new FakeNumberingService();
        var orderService = new ProductionOrderService(ctx, numbering);
        var runService = new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), orderService);

        var (_, _, orderId) = await orderService.CreateAsync(material.MaterialItemId, 1000m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);
        var (_, _, runId) = await runService.CreateAsync(orderId.Value, wc.ProductionWorkCenterId, null, null, null, null, null);
        await runService.StartAsync(runId!.Value, null);

        return (material, wc, runService, orderService, runId.Value);
    }

    [Fact]
    public async Task GetSummaryAsync_TotalOutput_SumsAllOutputsInPeriod()
    {
        var ctx = CreateContext(nameof(GetSummaryAsync_TotalOutput_SumsAllOutputsInPeriod));
        var (material, wc, runService, _, runId) = await SeedRunningRunAsync(ctx);
        await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 30m, null, "Primary", null, null);
        await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 20m, null, "Primary", null, null);

        var reportService = new OperationsReportService(ctx);
        var summary = await reportService.GetSummaryAsync(PeriodType.Day, DateTime.Today, null, null, wc.ProductionWorkCenterId);

        Assert.Equal(50m, summary.TotalOutputQuantity);
    }

    [Fact]
    public async Task GetSummaryAsync_BreaksDownByQualityStatus_AcceptedRejectedPendingBlocked()
    {
        var ctx = CreateContext(nameof(GetSummaryAsync_BreaksDownByQualityStatus_AcceptedRejectedPendingBlocked));
        var (material, wc, runService, _, runId) = await SeedRunningRunAsync(ctx);

        var (_, _, b1) = await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 10m, null, "Primary", null, null);
        var (_, _, b2) = await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 20m, null, "Primary", null, null);
        var (_, _, b3) = await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 15m, null, "Primary", null, null);

        await runService.SubmitBatchForQcAsync(b1!.Value, null);
        await runService.ReleaseBatchAsync(b1.Value, null);

        await runService.SubmitBatchForQcAsync(b2!.Value, null);
        await runService.RejectBatchAsync(b2.Value, "دلیل تست", null);

        // b3 هنوز در Produced می‌ماند - جزو PendingQc

        var reportService = new OperationsReportService(ctx);
        var summary = await reportService.GetSummaryAsync(PeriodType.Day, DateTime.Today, null, null, wc.ProductionWorkCenterId);

        Assert.Equal(10m, summary.AcceptedQuantity);
        Assert.Equal(20m, summary.RejectedQuantity);
        Assert.Equal(15m, summary.PendingQcQuantity);
        Assert.Equal(45m, summary.TotalOutputQuantity);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesLoss_FromProductionLoss()
    {
        var ctx = CreateContext(nameof(GetSummaryAsync_IncludesLoss_FromProductionLoss));
        var (_, wc, runService, _, runId) = await SeedRunningRunAsync(ctx);
        await runService.RecordLossAsync(runId, 5m, null, "ProcessLoss", null, null);

        var reportService = new OperationsReportService(ctx);
        var summary = await reportService.GetSummaryAsync(PeriodType.Day, DateTime.Today, null, null, wc.ProductionWorkCenterId);

        Assert.Equal(5m, summary.TotalLossQuantity);
    }

    [Fact]
    public async Task GetSummaryAsync_NoTarget_VarianceIsNull_NotZero()
    {
        var ctx = CreateContext(nameof(GetSummaryAsync_NoTarget_VarianceIsNull_NotZero));
        var (material, wc, runService, _, runId) = await SeedRunningRunAsync(ctx);
        await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 10m, null, "Primary", null, null);

        var reportService = new OperationsReportService(ctx);
        var summary = await reportService.GetSummaryAsync(PeriodType.Day, DateTime.Today, null, null, wc.ProductionWorkCenterId);

        // طبق بند ۴ سند فاز: بدون هدف ثبت‌شده، Variance هرگز صفر نشان داده نمی‌شود (گمراه‌کننده می‌بود)
        Assert.Equal(0m, summary.TotalTargetQuantity);
        Assert.Null(summary.VarianceQuantity);
    }

    [Fact]
    public async Task GetSummaryAsync_WithTarget_ComputesVariance_FromProductionTarget()
    {
        var ctx = CreateContext(nameof(GetSummaryAsync_WithTarget_ComputesVariance_FromProductionTarget));
        var (material, wc, runService, _, runId) = await SeedRunningRunAsync(ctx);
        await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 80m, null, "Primary", null, null);

        var targetService = new ProductionTargetService(ctx);
        await targetService.CreateAsync(DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, null, 100m, null, null);

        var reportService = new OperationsReportService(ctx);
        var summary = await reportService.GetSummaryAsync(PeriodType.Day, DateTime.Today, null, null, wc.ProductionWorkCenterId);

        Assert.Equal(100m, summary.TotalTargetQuantity);
        Assert.Equal(-20m, summary.VarianceQuantity);
    }

    [Fact]
    public async Task GetSummaryAsync_OutputOutsidePeriod_IsExcluded()
    {
        var ctx = CreateContext(nameof(GetSummaryAsync_OutputOutsidePeriod_IsExcluded));
        var (material, wc, runService, _, runId) = await SeedRunningRunAsync(ctx);
        await runService.RecordOutputAsync(runId, material.MaterialItemId, null, 999m, null, "Primary", null, null);

        var reportService = new OperationsReportService(ctx);
        // فردا را می‌پرسیم - نباید خروجی امروز را ببیند
        var summary = await reportService.GetSummaryAsync(PeriodType.Day, DateTime.Today.AddDays(1), null, null, wc.ProductionWorkCenterId);

        Assert.Equal(0m, summary.TotalOutputQuantity);
    }

    [Fact]
    public void OperationsReportSummaryDto_HasNoGrossOrNetField()
    {
        // طبق بند ۴ سند فاز («Business Semantics Gate»): اثبات ساختاری - هیچ فیلد Gross/Net
        // در این DTO وجود ندارد، چون تعریف کسب‌وکاری آن هنوز حل‌نشده است.
        var properties = typeof(OperationsReportSummaryDto).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("GrossQuantity", properties);
        Assert.DoesNotContain("NetQuantity", properties);
    }
}
