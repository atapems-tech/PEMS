using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 9» بند ۹/۱۶: تست‌های بسط CriticalityFailureMaintenanceMatrix و
/// PredictiveMaintenanceService/ReliabilitySimulationService - هرگز مقدار جعلی.
/// </summary>
public class Phase9IntelligenceExtensionTests
{
    [Fact]
    public async Task GetCriticalityFailureMaintenanceMatrixAsync_ComputesRealStrategyCoveragePercent()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P9Matrix_{nameof(GetCriticalityFailureMaintenanceMatrixAsync_ComputesRealStrategyCoveragePercent)}");

        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "E1", Name = "E1", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        ctx.Equipments.Add(new Equipment { EquipmentId = 2, TagNumber = "E2", Name = "E2", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentCriticalityId = 1, EquipmentId = 1, CriticalityClass = "A", IsActive = true, CreatedDate = DateTime.Now });
        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentCriticalityId = 2, EquipmentId = 2, CriticalityClass = "A", IsActive = true, CreatedDate = DateTime.Now });
        // فقط یکی از دو تجهیز کلاس A استراتژی فعال دارد
        ctx.MaintenanceStrategies.Add(new MaintenanceStrategy { MaintenanceStrategyId = 1, EquipmentId = 1, StrategyType = "Calendar", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var matrix = await service.GetCriticalityFailureMaintenanceMatrixAsync();

        var rowA = matrix.Single(x => x.CriticalityClass == "A");
        Assert.Equal(2, rowA.EquipmentCount);
        Assert.Equal(1, rowA.StrategyCoveredEquipmentCount);
        Assert.Equal(50.0, rowA.StrategyCoveragePercent);
    }

    [Fact]
    public async Task GetCriticalityFailureMaintenanceMatrixAsync_HighCriticalityWithNoStrategyCoverage_ProducesFinding()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P9Matrix_{nameof(GetCriticalityFailureMaintenanceMatrixAsync_HighCriticalityWithNoStrategyCoverage_ProducesFinding)}");

        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "E1", Name = "E1", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentCriticalityId = 1, EquipmentId = 1, CriticalityClass = "A", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var matrix = await service.GetCriticalityFailureMaintenanceMatrixAsync();

        var rowA = matrix.Single(x => x.CriticalityClass == "A");
        Assert.Equal(0, rowA.StrategyCoveragePercent);
        Assert.Contains(rowA.Findings, f => f.Contains("Low Maintenance Strategy Coverage"));
    }

    [Fact]
    public async Task GetCriticalityFailureMaintenanceMatrixAsync_LowCriticality_NeverProducesHighCriticalityFindings()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P9Matrix_{nameof(GetCriticalityFailureMaintenanceMatrixAsync_LowCriticality_NeverProducesHighCriticalityFindings)}");

        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "E1", Name = "E1", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentCriticalityId = 1, EquipmentId = 1, CriticalityClass = "C", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var matrix = await service.GetCriticalityFailureMaintenanceMatrixAsync();

        var rowC = matrix.Single(x => x.CriticalityClass == "C");
        Assert.DoesNotContain(rowC.Findings, f => f.Contains("High Criticality"));
    }

    [Fact]
    public async Task PredictiveMaintenanceService_NoModels_EngineReadyButProductionNotReady()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P9Predictive_{nameof(PredictiveMaintenanceService_NoModels_EngineReadyButProductionNotReady)}");
        var service = new PredictiveMaintenanceService(ctx);

        var readiness = await service.GetReadinessAsync();

        Assert.Equal("READY", readiness.EngineStatus);
        Assert.Equal("NOT_READY", readiness.ProductionModelStatus);
        Assert.Equal(0, readiness.ModelCount);
    }

    [Fact]
    public async Task ReliabilitySimulationService_NoWeibullData_ReturnsNotReady_DoesNotSimulate()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P9Sim_{nameof(ReliabilitySimulationService_NoWeibullData_ReturnsNotReady_DoesNotSimulate)}");
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "E1", Name = "E1", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var ramWeibullService = new RamWeibullService(ctx, new ReliabilityService(ctx));
        var simulationService = new ReliabilitySimulationService(ramWeibullService);

        var result = await simulationService.SimulateAvailabilityAsync(1);

        Assert.Equal("NOT_READY", result.DataStatus);
        Assert.NotNull(result.Reason);
        Assert.Null(result.MeanAvailabilityPercent);
        Assert.Equal("SIMULATED", result.Provenance);
    }

    [Fact]
    public async Task ReliabilitySimulationService_WithSufficientWeibullAndMttr_RunsAndReportsReady()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"P9Sim_{nameof(ReliabilitySimulationService_WithSufficientWeibullAndMttr_RunsAndReportsReady)}");
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "E1", Name = "E1", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });

        var start = new DateTime(2026, 1, 1);
        for (var i = 0; i < 8; i++)
        {
            // فاصله‌های متغیر (نه ثابت) لازم است - Median Rank Regression روی مقادیر x یکسان
            // واریانس صفر دارد (sxx=0) و عمداً Null برمی‌گرداند (نه یک شیب جعلی).
            var failureDate = start.AddDays(i * 60 + i * i * 5);
            ctx.FailureRecords.Add(new FailureRecord
            {
                FailureRecordId = i + 1, EquipmentId = 1, WorkOrderId = i + 1,
                FailureDate = failureDate, FailureMode = "Wear", DowntimeHours = 4, IsActive = true, CreatedDate = DateTime.Now
            });
            ctx.WorkOrders.Add(new WorkOrder
            {
                WorkOrderId = i + 1, EquipmentId = 1, WorkOrderNumber = $"WO-{i + 1}", WorkOrderType = "Corrective", Status = "Closed",
                StartDate = failureDate, CompletionDate = failureDate.AddHours(4), IsActive = true, CreatedDate = DateTime.Now
            });
        }
        await ctx.SaveChangesAsync();

        var ramWeibullService = new RamWeibullService(ctx, new ReliabilityService(ctx));
        var simulationService = new ReliabilitySimulationService(ramWeibullService);

        var result = await simulationService.SimulateAvailabilityAsync(1, iterations: 200, horizonHours: 8760);

        Assert.Equal("READY", result.DataStatus);
        Assert.NotNull(result.MeanAvailabilityPercent);
        Assert.InRange(result.MeanAvailabilityPercent!.Value, 0, 100);
        Assert.NotNull(result.P10AvailabilityPercent);
        Assert.NotNull(result.P90AvailabilityPercent);
        Assert.True(result.P10AvailabilityPercent <= result.P90AvailabilityPercent);
        Assert.Equal("SIMULATED", result.Provenance);
    }
}
