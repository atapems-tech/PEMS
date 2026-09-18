using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 5 — Final Reliability Intelligence, Failure Analytics & RAM Foundation» بند
/// ۶۱-۶۲: همه‌ی داده‌های تست مصنوعی هستند و فقط داخل تست‌ها ساخته می‌شوند - هرگز در دیتابیس
/// واقعی درج نمی‌شوند. هر تست دقیقاً یک قاعده‌ی ریاضی/کیفیت‌داده را اثبات می‌کند: MTBF فقط با
/// ≥۲ خرابی، MTTR فقط از WorkOrder های با تاریخ واقعی، Availability فقط وقتی هر دو معتبرند،
/// Zero≠Null در همه‌جا.
/// </summary>
public class ReliabilityAnalyticsTests
{
    private static async Task<PEMS.Persistence.Context.PEMSDbContext> SeedFullBaselineAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase5_{dbName}");
        var services = BuildProvider(ctx);

        await DisciplineSeeder.SeedAsync(services);
        await EquipmentClassSeeder.SeedAsync(services);
        await FailureCauseSeeder.SeedAsync(services);
        await FailureEffectSeeder.SeedAsync(services);
        await FailureConsequenceSeeder.SeedAsync(services);
        await FailureModeSeeder.SeedAsync(services);

        return ctx;
    }

    private static IServiceProvider BuildProvider(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        return services.BuildServiceProvider();
    }

    private static async Task<Equipment> AddEquipmentAsync(PEMS.Persistence.Context.PEMSDbContext ctx, int id, int? equipmentClassId = null)
    {
        var eq = new Equipment
        {
            EquipmentId = id, TagNumber = $"EQ-{id}", Name = $"EQ-{id}", EquipmentType = "pump",
            EquipmentClassId = equipmentClassId, ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();
        return eq;
    }

    // ================= MTTR =================

    [Fact]
    public async Task MTTR_MultipleRepairEvents_AveragesActualDurations()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTTR_MultipleRepairEvents_AveragesActualDurations));
        await AddEquipmentAsync(ctx, 1);

        var wo1 = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 1, 1, 8, 0, 0), CompletionDate = new DateTime(2026, 1, 1, 10, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        var wo2 = new WorkOrder { WorkOrderId = 2, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 2, 1, 8, 0, 0), CompletionDate = new DateTime(2026, 2, 1, 12, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.AddRange(wo1, wo2);
        await ctx.SaveChangesAsync();

        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", DowntimeHours = 2, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, WorkOrderId = 2, FailureDate = new DateTime(2026, 2, 1), FailureMode = "Leakage", DowntimeHours = 4, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(3.0, dto!.MTTRHours); // (2h + 4h) / 2
    }

    [Fact]
    public async Task MTTR_MissingRepairDuration_ReturnsNull_NeverZero()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTTR_MissingRepairDuration_ReturnsNull_NeverZero));
        await AddEquipmentAsync(ctx, 1);

        // WorkOrder بدون StartDate/CompletionDate واقعی (طبق داده‌ی واقعی لایو - Open مانده)
        ctx.WorkOrders.Add(new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Open", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Null(dto!.MTTRHours); // هرگز صفر جعلی
    }

    // ================= MTBF =================

    [Fact]
    public async Task MTBF_SingleFailure_ReturnsNull_InsufficientData()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTBF_SingleFailure_ReturnsNull_InsufficientData));
        await AddEquipmentAsync(ctx, 1);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.FailureCount);
        Assert.Null(dto.MTBFHours); // یک خرابی برای فاصله‌ی زمانی کافی نیست
    }

    [Fact]
    public async Task MTBF_TwoFailures_ComputesSpanBetweenThem()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTBF_TwoFailures_ComputesSpanBetweenThem));
        await AddEquipmentAsync(ctx, 1);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 11), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(240.0, dto!.MTBFHours); // ۱۰ روز = ۲۴۰ ساعت، تقسیم بر (۲-۱)=۱
    }

    [Fact]
    public async Task Reliability_NoFailures_AllMetricsNull_FailureCountZero_NotConflated()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Reliability_NoFailures_AllMetricsNull_FailureCountZero_NotConflated));
        await AddEquipmentAsync(ctx, 1);

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(0, dto!.FailureCount); // صفر واقعی: هیچ خرابی‌ای رخ نداده
        Assert.Null(dto.MTBFHours);
        Assert.Null(dto.MTTRHours);
        Assert.Null(dto.AvailabilityPercent);
    }

    // ================= Availability =================

    [Fact]
    public async Task Availability_OnlyComputedWhenBothMTBFAndMTTRValid()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Availability_OnlyComputedWhenBothMTBFAndMTTRValid));
        await AddEquipmentAsync(ctx, 1);

        var wo1 = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 1, 1, 0, 0, 0), CompletionDate = new DateTime(2026, 1, 1, 10, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        var wo2 = new WorkOrder { WorkOrderId = 2, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 2, 1, 0, 0, 0), CompletionDate = new DateTime(2026, 2, 1, 10, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.AddRange(wo1, wo2);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, WorkOrderId = 2, FailureDate = new DateTime(2026, 2, 1), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.MTBFHours);
        Assert.NotNull(dto.MTTRHours);
        Assert.NotNull(dto.AvailabilityPercent);
        Assert.Equal(dto.MTBFHours!.Value / (dto.MTBFHours.Value + dto.MTTRHours!.Value) * 100, dto.AvailabilityPercent);
    }

    // ================= Data Quality =================

    [Fact]
    public async Task DataQuality_TracksMissingFailureModeAndDowntime()
    {
        var ctx = await SeedFullBaselineAsync(nameof(DataQuality_TracksMissingFailureModeAndDowntime));
        await AddEquipmentAsync(ctx, 1);
        await AddEquipmentAsync(ctx, 2);

        // یکی کامل، یکی بدون FailureModeId/DowntimeHours
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", FailureModeId = null, DowntimeHours = null, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityDataQualityService(ctx);
        var dto = await service.GetDataQualityAsync();

        Assert.Equal(2, dto.TotalActiveEquipment);
        Assert.Equal(1, dto.EquipmentWithFailureHistory);
        Assert.Equal(1, dto.TotalFailureRecords);
        Assert.Equal(1, dto.FailureRecordsWithoutFailureMode);
        Assert.Equal(1, dto.FailureRecordsWithoutDowntimeHours);
        Assert.Equal(50.0, dto.EquipmentReliabilityCoveragePercent); // 1/2
    }

    [Fact]
    public async Task DataQuality_EmptyDatabase_ReturnsZeroCoverage_NotError()
    {
        var ctx = await SeedFullBaselineAsync(nameof(DataQuality_EmptyDatabase_ReturnsZeroCoverage_NotError));

        var service = new ReliabilityDataQualityService(ctx);
        var dto = await service.GetDataQualityAsync();

        Assert.Equal(0, dto.TotalActiveEquipment);
        Assert.Equal(0, dto.EquipmentReliabilityCoveragePercent); // نه یک استثنا تقسیم‌بر‌صفر
    }

    [Fact]
    public async Task DataQuality_NegativeRepairDuration_IsDetected()
    {
        var ctx = await SeedFullBaselineAsync(nameof(DataQuality_NegativeRepairDuration_IsDetected));
        await AddEquipmentAsync(ctx, 1);

        // CompletionDate قبل از StartDate - داده‌ی نامعتبر
        ctx.WorkOrders.Add(new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 1, 5), CompletionDate = new DateTime(2026, 1, 1), IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityDataQualityService(ctx);
        var dto = await service.GetDataQualityAsync();

        Assert.Equal(1, dto.FailureRecordsWithNegativeDuration);
    }

    // ================= Class Reliability =================

    [Fact]
    public async Task ClassReliability_AggregatesAcrossEquipmentInSameClass()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ClassReliability_AggregatesAcrossEquipmentInSameClass));
        var pumpClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        await AddEquipmentAsync(ctx, 1, pumpClass.EquipmentClassId);
        await AddEquipmentAsync(ctx, 2, pumpClass.EquipmentClassId);

        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Leakage", DowntimeHours = 2, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 2, FailureDate = new DateTime(2026, 1, 2), FailureMode = "Leakage", DowntimeHours = 3, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var analyticsService = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var dto = await analyticsService.GetClassReliabilityAsync(pumpClass.EquipmentClassId);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.EquipmentPopulation);
        Assert.Equal(2, dto.EquipmentWithFailureHistory);
        Assert.Equal(2, dto.FailureCount);
        Assert.Equal(5.0m, dto.TotalDowntimeHours);
    }

    [Fact]
    public async Task ClassReliability_ClassWithNoFailureHistory_ReturnsPopulationOnly_NotNull()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ClassReliability_ClassWithNoFailureHistory_ReturnsPopulationOnly_NotNull));
        var pumpClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        await AddEquipmentAsync(ctx, 1, pumpClass.EquipmentClassId);

        var analyticsService = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var dto = await analyticsService.GetClassReliabilityAsync(pumpClass.EquipmentClassId);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.EquipmentPopulation);
        Assert.Equal(0, dto.EquipmentWithFailureHistory);
        Assert.Null(dto.AverageMTBFHours);
    }

    // ================= Pareto =================

    [Fact]
    public async Task Pareto_OrdersDescendingAndComputesCumulativePercent()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Pareto_OrdersDescendingAndComputesCumulativePercent));
        await AddEquipmentAsync(ctx, 1);
        var bearingMode = await ctx.FailureModes.FirstAsync(x => x.Code == "PUMP_BEARING_FAILURE");
        var sealMode = await ctx.FailureModes.FirstAsync(x => x.Code == "PUMP_SEAL_LEAKAGE");

        // Bearing: 3 رویداد، Seal: 1 رویداد
        for (int i = 1; i <= 3; i++)
            ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = i, EquipmentId = 1, FailureDate = new DateTime(2026, 1, i), FailureMode = "Bearing", FailureModeId = bearingMode.FailureModeId, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 4, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 4), FailureMode = "Seal", FailureModeId = sealMode.FailureModeId, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var pareto = await service.GetFailureModeParetoAsync(null, null);

        Assert.Equal(2, pareto.Count);
        Assert.Equal(bearingMode.Name, pareto[0].Key);
        Assert.Equal(3, pareto[0].Count);
        Assert.Equal(75.0, pareto[0].CumulativePercent); // 3/4
        Assert.Equal(100.0, pareto[1].CumulativePercent); // 4/4
    }

    [Fact]
    public async Task Pareto_DateRangeFilter_ExcludesOutOfRangeEvents()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Pareto_DateRangeFilter_ExcludesOutOfRangeEvents));
        await AddEquipmentAsync(ctx, 1);
        var bearingMode = await ctx.FailureModes.FirstAsync(x => x.Code == "PUMP_BEARING_FAILURE");

        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2025, 1, 1), FailureMode = "Bearing", FailureModeId = bearingMode.FailureModeId, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "Bearing", FailureModeId = bearingMode.FailureModeId, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var pareto = await service.GetFailureModeParetoAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.Single(pareto);
        Assert.Equal(1, pareto[0].Count); // فقط رویداد ۲۰۲۶
    }

    // ================= Bad Actors =================

    [Fact]
    public async Task BadActors_RankedByFailureCountThenDowntime()
    {
        var ctx = await SeedFullBaselineAsync(nameof(BadActors_RankedByFailureCountThenDowntime));
        await AddEquipmentAsync(ctx, 1);
        await AddEquipmentAsync(ctx, 2);

        // Equipment 1: ۲ خرابی؛ Equipment 2: ۱ خرابی با Downtime بیشتر
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", DowntimeHours = 1, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 2), FailureMode = "X", DowntimeHours = 1, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 3, EquipmentId = 2, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", DowntimeHours = 100, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var badActors = await service.GetBadActorsAsync(null, null);

        Assert.Equal(2, badActors.Count);
        Assert.Equal("EQ-1", badActors[0].TagNumber); // بیشترین تعداد خرابی برنده است، نه بیشترین Downtime
    }

    [Fact]
    public async Task BadActors_EmptyFleet_ReturnsEmptyList_NoException()
    {
        var ctx = await SeedFullBaselineAsync(nameof(BadActors_EmptyFleet_ReturnsEmptyList_NoException));

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var badActors = await service.GetBadActorsAsync(null, null);

        Assert.Empty(badActors);
    }

    // ================= Trend =================

    [Fact]
    public async Task Trend_GroupsFailuresByMonth()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Trend_GroupsFailuresByMonth));
        await AddEquipmentAsync(ctx, 1);

        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 5), FailureMode = "X", DowntimeHours = 2, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 20), FailureMode = "X", DowntimeHours = 3, IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 3, EquipmentId = 1, FailureDate = new DateTime(2026, 2, 1), FailureMode = "X", DowntimeHours = 1, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var trend = await service.GetMonthlyTrendAsync(null, null);

        Assert.Equal(2, trend.Count);
        Assert.Equal("2026-01", trend[0].PeriodLabel);
        Assert.Equal(2, trend[0].FailureCount);
        Assert.Equal(5.0m, trend[0].TotalDowntimeHours);
        Assert.Equal("2026-02", trend[1].PeriodLabel);
        Assert.Equal(1, trend[1].FailureCount);
    }

    [Fact]
    public async Task Trend_NoData_ReturnsEmptyList_NotFabricatedPeriods()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Trend_NoData_ReturnsEmptyList_NotFabricatedPeriods));

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var trend = await service.GetMonthlyTrendAsync(null, null);

        Assert.Empty(trend);
    }

    // ================= Increment 3C - MTBF edge cases =================

    [Fact]
    public async Task MTBF_ThreeFailures_DividesTotalSpanByCountMinusOne()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTBF_ThreeFailures_DividesTotalSpanByCountMinusOne));
        await AddEquipmentAsync(ctx, 1);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 6), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 3, EquipmentId = 1, FailureDate = new DateTime(2026, 1, 11), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(3, dto!.FailureCount);
        Assert.Equal(120.0, dto.MTBFHours); // ۱۰ روز = ۲۴۰ ساعت / (۳-۱)=۲ => ۱۲۰
    }

    [Fact]
    public async Task MTBF_DuplicateFailureTimestamps_ReturnsExactZero_NotNull()
    {
        // طبق بند ۵۰ (Zero != Null): وقتی FirstFailureDate == LastFailureDate، فاصله‌ی زمانی
        // صفر است - این یک صفر واقعی است، نه نشانه‌ی «داده‌ی کافی نیست» (که Null می‌بود).
        var ctx = await SeedFullBaselineAsync(nameof(MTBF_DuplicateFailureTimestamps_ReturnsExactZero_NotNull));
        await AddEquipmentAsync(ctx, 1);
        var sameInstant = new DateTime(2026, 1, 1, 8, 0, 0);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = sameInstant, FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = sameInstant, FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.MTBFHours);
        Assert.Equal(0.0, dto.MTBFHours!.Value);
    }

    [Fact]
    public async Task MTBF_FutureDatedFailure_IncludedInSpan_NoExclusionApplied()
    {
        // ثابت می‌کند Gap G-5: تاریخ خرابی آینده از محاسبه‌ی MTBF حذف نمی‌شود - فقط به عنوان
        // یک معیار کیفیت داده‌ی جدا (FailureRecordsWithInvalidDate) ردیابی می‌شود، نه اینجا.
        var ctx = await SeedFullBaselineAsync(nameof(MTBF_FutureDatedFailure_IncludedInSpan_NoExclusionApplied));
        await AddEquipmentAsync(ctx, 1);
        var first = new DateTime(2026, 1, 1);
        var farFuture = new DateTime(2030, 1, 1); // نسبت به تاریخ سیستم (۲۰۲۶) کاملاً آینده است
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, FailureDate = first, FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, FailureDate = farFuture, FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        var expectedHours = (farFuture - first).TotalHours / 1; // (count-1) = 1
        Assert.NotNull(dto);
        Assert.Equal(farFuture, dto!.LastFailureDate);
        Assert.Equal(expectedHours, dto.MTBFHours);
    }

    // ================= Increment 3C - MTTR edge cases =================

    [Fact]
    public async Task MTTR_UnlinkedFailureRecord_ExcludedFromAverage_OnlyLinkedFailuresCounted()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTTR_UnlinkedFailureRecord_ExcludedFromAverage_OnlyLinkedFailuresCounted));
        await AddEquipmentAsync(ctx, 1);

        var wo = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 1, 1, 8, 0, 0), CompletionDate = new DateTime(2026, 1, 1, 13, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.Add(wo);
        // خرابی ۱: مرتبط با WorkOrder (۵ ساعت). خرابی ۲: بدون WorkOrderId (باید از میانگین حذف شود، نه صفر جعلی).
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, WorkOrderId = null, FailureDate = new DateTime(2026, 2, 1), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.FailureCount); // هر دو در FailureCount/MTBF حساب می‌شوند
        Assert.Equal(5.0, dto.MTTRHours); // اما فقط رکورد مرتبط در MTTR
    }

    [Fact]
    public async Task MTTR_NegativeDurationWorkOrder_NotExcludedOrClamped_ProvesGapG1()
    {
        // ثابت می‌کند Gap G-1: هیچ محافظی برای CompletionDate < StartDate در محاسبه‌ی MTTR
        // وجود ندارد - با اینکه ReliabilityDataQualityService دقیقاً همین شرط را به عنوان یک
        // مشکل کیفیت داده ردیابی می‌کند (FailureRecordsWithNegativeDuration)، ReliabilityService
        // آن را از میانگین MTTR حذف یا Clamp نمی‌کند.
        var ctx = await SeedFullBaselineAsync(nameof(MTTR_NegativeDurationWorkOrder_NotExcludedOrClamped_ProvesGapG1));
        await AddEquipmentAsync(ctx, 1);

        var wo = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 1, 1, 10, 0, 0), CompletionDate = new DateTime(2026, 1, 1, 8, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.Add(wo);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.MTTRHours);
        Assert.Equal(-2.0, dto.MTTRHours!.Value); // منفی، نه Null و نه Clamp‌شده به صفر
    }

    [Fact]
    public async Task MTTR_ZeroDurationWorkOrder_CountsAsExactZero_NotNull()
    {
        var ctx = await SeedFullBaselineAsync(nameof(MTTR_ZeroDurationWorkOrder_CountsAsExactZero_NotNull));
        await AddEquipmentAsync(ctx, 1);

        var sameInstant = new DateTime(2026, 1, 1, 8, 0, 0);
        var wo = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = sameInstant, CompletionDate = sameInstant, IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.Add(wo);
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.NotNull(dto!.MTTRHours);
        Assert.Equal(0.0, dto.MTTRHours!.Value);
    }

    // ================= Increment 3C - Availability edge cases =================

    [Fact]
    public async Task Availability_NegativeMTTR_CanExceedOneHundredPercent_ProvesGapG1()
    {
        // ثابت می‌کند Availability به بازه‌ی ۰-۱۰۰٪ محدود نیست: محافظ فعلی فقط از تقسیم بر صفر
        // جلوگیری می‌کند ((MTBF+MTTR) > 0)، نه از علامت یا اندازه‌ی MTTR.
        var ctx = await SeedFullBaselineAsync(nameof(Availability_NegativeMTTR_CanExceedOneHundredPercent_ProvesGapG1));
        await AddEquipmentAsync(ctx, 1);

        var wo = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = new DateTime(2026, 1, 1, 10, 0, 0), CompletionDate = new DateTime(2026, 1, 1, 8, 0, 0), IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.Add(wo);
        // ۲ خرابی به فاصله‌ی ۱۰ روز => MTBF=240h. خرابی اول به یک WorkOrder با مدت منفی (-۲h) مرتبط است؛
        // خرابی دوم بدون WorkOrder است، پس MTTR = میانگین فقط همان -۲h = -۲.
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, WorkOrderId = null, FailureDate = new DateTime(2026, 1, 11), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(240.0, dto!.MTBFHours);
        Assert.Equal(-2.0, dto.MTTRHours);
        Assert.NotNull(dto.AvailabilityPercent);
        Assert.True(dto.AvailabilityPercent!.Value > 100.0, $"Expected Availability > 100% (unbounded gap), got {dto.AvailabilityPercent}");
    }

    [Fact]
    public async Task Availability_MTBFAndMTTRBothExactlyZero_GuardPreventsZeroDivision_ReturnsNull()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Availability_MTBFAndMTTRBothExactlyZero_GuardPreventsZeroDivision_ReturnsNull));
        await AddEquipmentAsync(ctx, 1);

        var sameInstant = new DateTime(2026, 1, 1, 8, 0, 0);
        var wo = new WorkOrder { WorkOrderId = 1, EquipmentId = 1, WorkOrderType = "Corrective", Status = "Closed", StartDate = sameInstant, CompletionDate = sameInstant, IsActive = true, CreatedDate = DateTime.Now };
        ctx.WorkOrders.Add(wo);
        // هر دو خرابی دقیقاً همزمان (MTBF=0) و WorkOrder با مدت دقیقاً صفر (MTTR=0)
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 1, EquipmentId = 1, WorkOrderId = 1, FailureDate = sameInstant, FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = 2, EquipmentId = 1, WorkOrderId = null, FailureDate = sameInstant, FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityService(ctx);
        var dto = await service.GetEquipmentReliabilityAsync(1);

        Assert.NotNull(dto);
        Assert.Equal(0.0, dto!.MTBFHours);
        Assert.Equal(0.0, dto.MTTRHours);
        Assert.Null(dto.AvailabilityPercent); // محافظ (MTBF+MTTR)>0 از تقسیم ۰/۰ جلوگیری می‌کند
    }

    // ================= Increment 3C - Bad Actor edge cases =================

    [Fact]
    public async Task BadActors_ThreeWayTieOnCountAndDowntime_OrderIsStableAcrossRepeatedCalls()
    {
        // ثابت می‌کند Gap G-3: هیچ کلید تفکیک صریح سوم (مثل EquipmentId) وجود ندارد - ترتیب
        // فعلی صرفاً از پایداری Sort کتابخانه‌ی LINQ و ترتیب ورودی FailureRecords ناشی می‌شود،
        // نه یک قاعده‌ی صریح مستند‌شده. این تست فقط «پایداری بین دو فراخوانی» را اثبات می‌کند،
        // نه اینکه کدام تجهیز برنده می‌شود.
        var ctx = await SeedFullBaselineAsync(nameof(BadActors_ThreeWayTieOnCountAndDowntime_OrderIsStableAcrossRepeatedCalls));
        await AddEquipmentAsync(ctx, 1);
        await AddEquipmentAsync(ctx, 2);
        await AddEquipmentAsync(ctx, 3);

        var id = 1;
        foreach (var equipmentId in new[] { 1, 2, 3 })
        {
            ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = id++, EquipmentId = equipmentId, FailureDate = new DateTime(2026, 1, 1), FailureMode = "X", DowntimeHours = 5, IsActive = true, CreatedDate = DateTime.Now });
            ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = id++, EquipmentId = equipmentId, FailureDate = new DateTime(2026, 1, 2), FailureMode = "X", DowntimeHours = 5, IsActive = true, CreatedDate = DateTime.Now });
        }
        await ctx.SaveChangesAsync();

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var firstCall = await service.GetBadActorsAsync(null, null);
        var secondCall = await service.GetBadActorsAsync(null, null);

        Assert.Equal(3, firstCall.Count);
        Assert.Equal(firstCall.Select(x => x.EquipmentId), secondCall.Select(x => x.EquipmentId));
    }

    [Fact]
    public async Task BadActors_TopNParameter_LimitsResultCountToRequestedTop()
    {
        var ctx = await SeedFullBaselineAsync(nameof(BadActors_TopNParameter_LimitsResultCountToRequestedTop));
        for (var equipmentId = 1; equipmentId <= 5; equipmentId++)
        {
            await AddEquipmentAsync(ctx, equipmentId);
            for (var f = 1; f <= equipmentId; f++) // Equipment N گیرد N خرابی (رتبه‌بندی متمایز)
                ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = equipmentId * 10 + f, EquipmentId = equipmentId, FailureDate = new DateTime(2026, 1, f), FailureMode = "X", IsActive = true, CreatedDate = DateTime.Now });
        }
        await ctx.SaveChangesAsync();

        var service = new ReliabilityAnalyticsService(ctx, new ReliabilityService(ctx));
        var badActors = await service.GetBadActorsAsync(null, null, top: 3);

        Assert.Equal(3, badActors.Count);
        Assert.Equal("EQ-5", badActors[0].TagNumber); // بیشترین خرابی (۵) اول
        Assert.Equal("EQ-4", badActors[1].TagNumber);
        Assert.Equal("EQ-3", badActors[2].TagNumber);
    }
}
