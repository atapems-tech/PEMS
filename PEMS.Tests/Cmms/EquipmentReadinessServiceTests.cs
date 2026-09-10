using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 7.5 — CMMS Operational Data Readiness & Master Data Activation»: تست‌های
/// آماده‌بودی سطح-تجهیز (Classification/Criticality/Strategy/PmPlan/JobPlan/Safety/Execution)
/// و Class Activation Backlog. هیچ کدام مقدار جعل نمی‌کند - هر بُعد دقیقاً منعکس‌کننده‌ی وجود/
/// عدم‌وجود رکورد واقعی است.
/// </summary>
public class EquipmentReadinessServiceTests
{
    private static EquipmentReadinessService CreateService(PEMS.Persistence.Context.PEMSDbContext ctx) =>
        new(ctx, new EquipmentClassCoverageService(ctx));

    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, int ClassId)> SeedClassAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase75_{dbName}");
        var equipmentClass = new EquipmentClass { Code = "TEST_CLASS", Name = "Test Class", IsActive = true };
        ctx.EquipmentClasses.Add(equipmentClass);
        await ctx.SaveChangesAsync();
        return (ctx, equipmentClass.EquipmentClassId);
    }

    // ================= GetOperationalReadinessSummaryAsync =================

    [Fact]
    public async Task Summary_EmptyDatabase_AllCountsZero()
    {
        var (ctx, _) = await SeedClassAsync(nameof(Summary_EmptyDatabase_AllCountsZero));
        var service = CreateService(ctx);

        var summary = await service.GetOperationalReadinessSummaryAsync();

        Assert.Equal(0, summary.TotalActiveEquipment);
        Assert.Equal(0, summary.ExecutionReadyEquipment);
    }

    [Fact]
    public async Task Summary_UnclassifiedEquipment_CountedSeparatelyFromClassified()
    {
        var (ctx, classId) = await SeedClassAsync(nameof(Summary_UnclassifiedEquipment_CountedSeparatelyFromClassified));
        ctx.Equipments.AddRange(
            new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId },
            new Equipment { TagNumber = "EQ-2", Name = "EQ-2", EquipmentType = "Pump", IsActive = true, EquipmentClassId = null }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var summary = await service.GetOperationalReadinessSummaryAsync();

        Assert.Equal(2, summary.TotalActiveEquipment);
        Assert.Equal(1, summary.ClassifiedEquipment);
        Assert.Equal(1, summary.UnclassifiedEquipment);
    }

    [Fact]
    public async Task Summary_ExecutionReady_RequiresAllFiveDimensions()
    {
        var (ctx, classId) = await SeedClassAsync(nameof(Summary_ExecutionReady_RequiresAllFiveDimensions));
        var equipment = new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // فقط Classified - هنوز آماده نیست
        var summary1 = await service.GetOperationalReadinessSummaryAsync();
        Assert.Equal(0, summary1.ExecutionReadyEquipment);

        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentId = equipment.EquipmentId, CriticalityClass = "B", IsActive = true });
        ctx.MaintenanceStrategies.Add(new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "Preventive", IsActive = true });
        await ctx.SaveChangesAsync();

        // Criticality + Strategy موجودند ولی هنوز Plan نیست
        var summary2 = await service.GetOperationalReadinessSummaryAsync();
        Assert.Equal(0, summary2.ExecutionReadyEquipment);

        var jobPlan = new JobPlan { JobPlanNumber = "JP-1", Title = "Test", IsActive = true };
        ctx.JobPlans.Add(jobPlan);
        await ctx.SaveChangesAsync();
        ctx.MaintenancePlans.Add(new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = ctx.MaintenanceStrategies.Single().MaintenanceStrategyId,
            PlanNumber = "MP-1", Title = "Test Plan", TriggerType = "Calendar", JobPlanId = jobPlan.JobPlanId, IsActive = true
        });
        await ctx.SaveChangesAsync();

        // حالا همه‌ی پنج بُعد موجودند
        var summary3 = await service.GetOperationalReadinessSummaryAsync();
        Assert.Equal(1, summary3.ExecutionReadyEquipment);
        Assert.Equal(1, summary3.JobPlanReadyEquipment);
        Assert.Equal(1, summary3.SafetyReadyEquipment);
    }

    [Fact]
    public async Task Summary_PlanWithoutJobPlanLink_NotJobPlanReady()
    {
        // طبق بند ۱۴: Plan خالی (بدون JobPlanId) کافی نیست - JOB_PLAN_REQUIRED باقی می‌ماند
        var (ctx, classId) = await SeedClassAsync(nameof(Summary_PlanWithoutJobPlanLink_NotJobPlanReady));
        var equipment = new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "Preventive", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        ctx.MaintenancePlans.Add(new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-1", Title = "Test Plan", TriggerType = "Calendar", JobPlanId = null, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var summary = await service.GetOperationalReadinessSummaryAsync();

        Assert.Equal(1, summary.PmPlanReadyEquipment);
        Assert.Equal(0, summary.JobPlanReadyEquipment);
        Assert.Equal(0, summary.ExecutionReadyEquipment);
    }

    // ================= GetEquipmentReadinessAsync =================

    [Fact]
    public async Task EquipmentReadiness_NoDependenciesAtAll_ReturnsNotReadyAndNotApplicable()
    {
        var (ctx, classId) = await SeedClassAsync(nameof(EquipmentReadiness_NoDependenciesAtAll_ReturnsNotReadyAndNotApplicable));
        ctx.Equipments.Add(new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.GetEquipmentReadinessAsync(classId);

        var row = Assert.Single(result.Items);
        Assert.Equal(ReadinessState.Ready, row.Classification);
        Assert.Equal(ReadinessState.NotReady, row.Criticality);
        Assert.Equal(ReadinessState.NotReady, row.Strategy);
        Assert.Equal(ReadinessState.NotReady, row.PmPlan);
        Assert.Equal(ReadinessState.NotApplicable, row.JobPlan); // بدون Plan، چیزی برای ارزیابی نیست
        Assert.Equal(ReadinessState.NotApplicable, row.Safety);
        Assert.False(row.ExecutionReady);
    }

    [Fact]
    public async Task EquipmentReadiness_PlanExistsButNoJobPlanLink_ReturnsMissingMasterDataAndBlockedBySafety()
    {
        var (ctx, classId) = await SeedClassAsync(nameof(EquipmentReadiness_PlanExistsButNoJobPlanLink_ReturnsMissingMasterDataAndBlockedBySafety));
        var equipment = new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "Preventive", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        ctx.MaintenancePlans.Add(new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-1", Title = "Test Plan", TriggerType = "Calendar", JobPlanId = null, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.GetEquipmentReadinessAsync(classId);

        var row = Assert.Single(result.Items);
        Assert.Equal(ReadinessState.MissingMasterData, row.JobPlan);
        Assert.Equal(ReadinessState.BlockedBySafety, row.Safety);
    }

    [Fact]
    public async Task EquipmentReadiness_FullyConfigured_ReturnsExecutionReady()
    {
        var (ctx, classId) = await SeedClassAsync(nameof(EquipmentReadiness_FullyConfigured_ReturnsExecutionReady));
        var equipment = new Equipment { TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId };
        ctx.Equipments.Add(equipment);
        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentId = equipment.EquipmentId, CriticalityClass = "A", IsActive = true });
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "Preventive", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        var jobPlan = new JobPlan { JobPlanNumber = "JP-1", Title = "Test", IsActive = true };
        ctx.JobPlans.Add(jobPlan);
        await ctx.SaveChangesAsync();
        ctx.MaintenancePlans.Add(new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-1", Title = "Test Plan", TriggerType = "Calendar", JobPlanId = jobPlan.JobPlanId, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.GetEquipmentReadinessAsync(classId);

        var row = Assert.Single(result.Items);
        Assert.True(row.ExecutionReady);
        Assert.Equal(ReadinessState.Ready, row.JobPlan);
        Assert.Equal(ReadinessState.Ready, row.Safety);
    }

    [Fact]
    public async Task EquipmentReadiness_Pagination_RespectsPageSize()
    {
        // طبق سند «PEMS — Global Data Grid Standard»: فقط {20, 50, 100} مجاز است
        // (DataGridPageSize.Normalize) - این تست دقیقاً همان قرارداد را رعایت می‌کند.
        var (ctx, classId) = await SeedClassAsync(nameof(EquipmentReadiness_Pagination_RespectsPageSize));
        for (var i = 0; i < 25; i++)
            ctx.Equipments.Add(new Equipment { TagNumber = $"EQ-{i:00}", Name = $"EQ-{i:00}", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classId });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.GetEquipmentReadinessAsync(classId, page: 1, pageSize: 20);

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
    }

    // ================= GetClassActivationBacklogAsync =================

    [Fact]
    public async Task Backlog_ClassWithNoEquipment_Excluded()
    {
        var (ctx, _) = await SeedClassAsync(nameof(Backlog_ClassWithNoEquipment_Excluded));
        var service = CreateService(ctx);

        var backlog = await service.GetClassActivationBacklogAsync();

        Assert.Empty(backlog);
    }

    [Fact]
    public async Task Backlog_ClassWithDefensibleBasis_RankedBeforeClassWithout()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase75_{nameof(Backlog_ClassWithDefensibleBasis_RankedBeforeClassWithout)}");

        var classWithPmTemplate = new EquipmentClass { Code = "WITH_PMT", Name = "With PM Template", IsActive = true };
        var classWithoutBasis = new EquipmentClass { Code = "WITHOUT_BASIS", Name = "Without Basis", IsActive = true };
        ctx.EquipmentClasses.AddRange(classWithPmTemplate, classWithoutBasis);
        await ctx.SaveChangesAsync();

        // کلاس بدون مبنا عمداً جمعیت بزرگ‌تری دارد - تا اثبات کند «مبنای مدافعه‌پذیر» بر جمعیت اولویت دارد
        for (var i = 0; i < 2; i++)
            ctx.Equipments.Add(new Equipment { TagNumber = $"PMT-{i}", Name = $"PMT-{i}", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classWithPmTemplate.EquipmentClassId });
        for (var i = 0; i < 20; i++)
            ctx.Equipments.Add(new Equipment { TagNumber = $"NB-{i}", Name = $"NB-{i}", EquipmentType = "Pump", IsActive = true, EquipmentClassId = classWithoutBasis.EquipmentClassId });
        await ctx.SaveChangesAsync();

        ctx.PMTemplates.Add(new PMTemplate
        {
            Code = "PMT-1", Name = "Test Template", EquipmentClassId = classWithPmTemplate.EquipmentClassId,
            TriggerType = "Calendar", IntervalDays = 30, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var backlog = await service.GetClassActivationBacklogAsync();

        Assert.Equal(2, backlog.Count);
        Assert.Equal("WITH_PMT", backlog[0].Code); // مبنای مدافعه‌پذیر، با وجود جمعیت کوچک‌تر، اول است
        Assert.True(backlog[0].HasDefensibleBasis);
        Assert.False(backlog[1].HasDefensibleBasis);
    }
}
