using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۷: رویداد واقعی توقف - جدا از
/// WorkOrder.DowntimeHours/FailureRecord.DowntimeHours. Duration همیشه مشتق می‌شود، هرگز
/// مستقیم ذخیره نمی‌شود.
/// </summary>
public class ProductionDowntimeServiceTests
{
    private class FakeNumberingService : PEMS.Application.Interfaces.INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"ProductionDowntime_{dbName}");

    private static async Task<(ProductionWorkCenter workCenter, int runId, Equipment equipment)> SeedBaselineAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var fl = new FunctionalLocation { Code = "FL-1", Name = "Area 100", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var equipment = new Equipment { TagNumber = "P-101", Name = "Reactor Pump", EquipmentType = "Pump", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.Equipments.Add(equipment);

        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);

        var workCenter = new ProductionWorkCenter { Code = "WC-1", Name = "Line 1", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.ProductionWorkCenters.Add(workCenter);
        await ctx.SaveChangesAsync();

        var numbering = new FakeNumberingService();
        var orderService = new ProductionOrderService(ctx, numbering);
        var (_, _, orderId) = await orderService.CreateAsync(material.MaterialItemId, 100m, null, workCenter.ProductionWorkCenterId, null, null, null, null, null, null);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);

        var runService = new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), orderService);
        var (_, _, runId) = await runService.CreateAsync(orderId.Value, workCenter.ProductionWorkCenterId, null, null, null, null, null);

        return (workCenter, runId!.Value, equipment);
    }

    [Fact]
    public async Task StartAsync_ValidInput_CreatesOpenEvent()
    {
        var ctx = CreateContext(nameof(StartAsync_ValidInput_CreatesOpenEvent));
        var (wc, runId, _) = await SeedBaselineAsync(ctx);
        var service = new ProductionDowntimeService(ctx);

        var (success, error, eventId) = await service.StartAsync(
            runId, wc.ProductionWorkCenterId, null, DateTime.Now, "Process", "Feed shortage", null, null, null);

        Assert.True(success, error);
        Assert.NotNull(eventId);

        var list = await service.GetForRunAsync(runId);
        Assert.Single(list);
        Assert.Null(list[0].EndTime);
        Assert.Null(list[0].DurationMinutes);
    }

    [Fact]
    public async Task StartAsync_EquipmentIdOptional_NullIsAccepted()
    {
        var ctx = CreateContext(nameof(StartAsync_EquipmentIdOptional_NullIsAccepted));
        var (wc, runId, _) = await SeedBaselineAsync(ctx);
        var service = new ProductionDowntimeService(ctx);

        // طبق بخش ۷ سند فاز: EquipmentId اجباری نیست - توقف عملیاتی (مثلاً کمبود ماده) لزوماً
        // به خرابی تجهیز نگاشت نمی‌شود
        var (success, error, _) = await service.StartAsync(
            runId, wc.ProductionWorkCenterId, null, DateTime.Now, "Material", "Raw material shortage", null, null, null);

        Assert.True(success, error);
    }

    [Fact]
    public async Task StartAsync_InvalidCauseCategory_IsRejected()
    {
        var ctx = CreateContext(nameof(StartAsync_InvalidCauseCategory_IsRejected));
        var (wc, runId, _) = await SeedBaselineAsync(ctx);
        var service = new ProductionDowntimeService(ctx);

        var (success, _, _) = await service.StartAsync(
            runId, wc.ProductionWorkCenterId, null, DateTime.Now, "این یک دسته‌بندی دلخواه است", null, null, null, null);

        Assert.False(success);
    }

    [Fact]
    public async Task CloseAsync_DerivesDurationFromStartEnd_NeverStoredIndependently()
    {
        var ctx = CreateContext(nameof(CloseAsync_DerivesDurationFromStartEnd_NeverStoredIndependently));
        var (wc, runId, equipment) = await SeedBaselineAsync(ctx);
        var service = new ProductionDowntimeService(ctx);

        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        var end = start.AddMinutes(45);

        var (_, _, eventId) = await service.StartAsync(runId, wc.ProductionWorkCenterId, equipment.EquipmentId, start, "Equipment", null, null, null, null);
        var closeResult = await service.CloseAsync(eventId!.Value, end);

        Assert.True(closeResult.Success, closeResult.Error);

        var list = await service.GetForRunAsync(runId);
        var closed = Assert.Single(list);
        Assert.Equal(45m, closed.DurationMinutes);
    }

    [Fact]
    public async Task CloseAsync_EndTimeBeforeStartTime_IsRejected()
    {
        var ctx = CreateContext(nameof(CloseAsync_EndTimeBeforeStartTime_IsRejected));
        var (wc, runId, _) = await SeedBaselineAsync(ctx);
        var service = new ProductionDowntimeService(ctx);

        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        var (_, _, eventId) = await service.StartAsync(runId, wc.ProductionWorkCenterId, null, start, "Process", null, null, null, null);

        var closeResult = await service.CloseAsync(eventId!.Value, start.AddMinutes(-10));

        Assert.False(closeResult.Success);
    }

    [Fact]
    public async Task CloseAsync_AlreadyClosedEvent_IsRejected()
    {
        var ctx = CreateContext(nameof(CloseAsync_AlreadyClosedEvent_IsRejected));
        var (wc, runId, _) = await SeedBaselineAsync(ctx);
        var service = new ProductionDowntimeService(ctx);

        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        var (_, _, eventId) = await service.StartAsync(runId, wc.ProductionWorkCenterId, null, start, "Process", null, null, null, null);
        await service.CloseAsync(eventId!.Value, start.AddMinutes(10));

        var secondClose = await service.CloseAsync(eventId.Value, start.AddMinutes(20));

        Assert.False(secondClose.Success);
    }

    [Fact]
    public void ProductionDowntimeEvent_DoesNotDuplicateWorkOrderDowntimeHours()
    {
        // طبق بخش ۷ سند فاز: این Entity جایگزین WorkOrder.DowntimeHours نمی‌شود - فقط اثبات
        // ساختاری که هیچ فیلدی به آن اشاره/بازنویسی نمی‌کند.
        var properties = typeof(ProductionDowntimeEvent).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("DowntimeHours", properties);
        Assert.DoesNotContain("DurationMinutes", properties);
    }
}
