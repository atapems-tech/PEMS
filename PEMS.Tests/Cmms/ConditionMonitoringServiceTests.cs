using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۵: تست‌های زیرساخت نظارت وضعیت - هیچ قرائتی هرگز جعل نمی‌شود؛
/// Provenance همیشه OBSERVED برای قرائت واقعی است.
/// </summary>
public class ConditionMonitoringServiceTests
{
    private static async Task<(ConditionMonitoringService Service, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateServiceAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"ConditionMon_{dbName}");
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "P-101", Name = "Pump", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        return (new ConditionMonitoringService(ctx), ctx);
    }

    [Fact]
    public async Task GetFoundationStatusAsync_NoData_FoundationReady_LiveTelemetryNotReady()
    {
        var (service, _) = await CreateServiceAsync(nameof(GetFoundationStatusAsync_NoData_FoundationReady_LiveTelemetryNotReady));

        var status = await service.GetFoundationStatusAsync();

        Assert.Equal("READY", status.FoundationStatus);
        Assert.Equal("NOT_READY", status.LiveTelemetryStatus);
        Assert.Equal(0, status.MonitoringPointCount);
        Assert.Equal(0, status.ReadingCount);
    }

    [Fact]
    public async Task CreatePointAsync_InvalidVariableType_Fails()
    {
        var (service, _) = await CreateServiceAsync(nameof(CreatePointAsync_InvalidVariableType_Fails));

        var (success, error, id) = await service.CreatePointAsync(1, "NotARealVariable", "VT-101", null, null, null, null, "tester");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(id);
    }

    [Fact]
    public async Task CreatePointAsync_ValidVariableType_Succeeds()
    {
        var (service, _) = await CreateServiceAsync(nameof(CreatePointAsync_ValidVariableType_Succeeds));

        var (success, error, id) = await service.CreatePointAsync(1, "Vibration", "VT-101", "mm/s", 0, 10, "test point", "tester");

        Assert.True(success, error);
        Assert.NotNull(id);
    }

    [Fact]
    public async Task RecordReadingAsync_WithinThresholds_NotFlaggedAsAlarm_ProvenanceIsObserved()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(RecordReadingAsync_WithinThresholds_NotFlaggedAsAlarm_ProvenanceIsObserved));
        var (_, _, pointId) = await service.CreatePointAsync(1, "Temperature", "TE-101", "C", 0, 100, null, "tester");

        var (success, error) = await service.RecordReadingAsync(pointId!.Value, DateTime.Now, 50, "Manual", null, "tester");

        Assert.True(success, error);
        var reading = ctx.ConditionReadings.Single();
        Assert.False(reading.IsAlarm);
        Assert.Equal("OBSERVED", reading.Provenance);
    }

    [Fact]
    public async Task RecordReadingAsync_AboveHighThreshold_FlaggedAsAlarm()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(RecordReadingAsync_AboveHighThreshold_FlaggedAsAlarm));
        var (_, _, pointId) = await service.CreatePointAsync(1, "Temperature", "TE-101", "C", 0, 100, null, "tester");

        await service.RecordReadingAsync(pointId!.Value, DateTime.Now, 150, "Manual", null, "tester");

        var reading = ctx.ConditionReadings.Single();
        Assert.True(reading.IsAlarm);
    }

    [Fact]
    public async Task RecordReadingAsync_UnknownPoint_Fails()
    {
        var (service, _) = await CreateServiceAsync(nameof(RecordReadingAsync_UnknownPoint_Fails));

        var (success, error) = await service.RecordReadingAsync(999, DateTime.Now, 10, "Manual", null, "tester");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task GetFoundationStatusAsync_WithRealReading_LiveTelemetryPartial()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetFoundationStatusAsync_WithRealReading_LiveTelemetryPartial));
        var (_, _, pointId) = await service.CreatePointAsync(1, "Vibration", "VT-101", "mm/s", null, null, null, "tester");
        await service.RecordReadingAsync(pointId!.Value, DateTime.Now, 5, "Manual", null, "tester");

        var status = await service.GetFoundationStatusAsync();

        Assert.Equal("PARTIAL", status.LiveTelemetryStatus);
        Assert.Equal(1, status.ReadingCount);
    }
}
