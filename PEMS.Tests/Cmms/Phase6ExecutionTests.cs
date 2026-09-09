using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6 — CMMS Operational Excellence & Maintenance Execution» بند ۴۵: تست‌های
/// سه شکاف واقعی که این فاز بست - Idempotency تولید PM، پل کنترل‌شده‌ی RCM→عملیاتی (بدون
/// ساخت خاموش)، و گیت آزادسازی WorkOrder (Permit/LOTO/GasTest طبق مدل داده‌ی موجود).
/// </summary>
public class Phase6ExecutionTests
{
    // NumberingService واقعی از UPDATE...OUTPUT خام SQL Server استفاده می‌کند (با InMemory کار
    // نمی‌کند) - این Fake ساده فقط شماره‌ی یکتای افزایشی برمی‌گرداند.
    private class FakeNumberingService : INumberingService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-{++_counter:0000}");
    }

    private static async Task<PEMS.Persistence.Context.PEMSDbContext> SeedFullBaselineAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase6_{dbName}");
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        var provider = services.BuildServiceProvider();

        await DisciplineSeeder.SeedAsync(provider);
        await MaintenanceGroupSeeder.SeedAsync(provider);
        await MaintenanceTypeSeeder.SeedAsync(provider);
        await MaintenanceStrategyTypeSeeder.SeedAsync(provider);
        await EquipmentClassSeeder.SeedAsync(provider);
        await StandardReferenceSeeder.SeedAsync(provider);
        await MaintenanceActivitySeeder.SeedAsync(provider);
        await FailureCauseSeeder.SeedAsync(provider);
        await FailureMechanismSeeder.SeedAsync(provider);
        await FailureEffectSeeder.SeedAsync(provider);
        await FailureConsequenceSeeder.SeedAsync(provider);
        await FailureCauseMechanismSeeder.SeedAsync(provider);
        await FailureModeSeeder.SeedAsync(provider);
        await PMTemplateSeeder.SeedAsync(provider);

        return ctx;
    }

    // ================= PMGenerationService: Idempotency =================

    [Fact]
    public async Task PMGeneration_GenerateForPlan_CreatesWorkOrder_WhenNoneOpen()
    {
        var ctx = await SeedFullBaselineAsync(nameof(PMGeneration_GenerateForPlan_CreatesWorkOrder_WhenNoneOpen));
        var equipment = new Equipment { TagNumber = "EQ-100", Name = "EQ-100", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "PM", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        var plan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-100", Title = "Monthly PM", TriggerType = "Calendar", IntervalDays = 30, IsActive = true
        };
        ctx.MaintenancePlans.Add(plan);
        await ctx.SaveChangesAsync();

        var service = new PMGenerationService(ctx, new FakeNumberingService());
        var workOrderNumber = await service.GenerateForPlanAsync(plan.MaintenancePlanId);

        Assert.NotNull(workOrderNumber);
        Assert.Single(ctx.WorkOrders);
        var workOrder = ctx.WorkOrders.Single();
        Assert.Equal(plan.MaintenancePlanId, workOrder.MaintenancePlanId);
        Assert.Equal("Open", workOrder.Status);
        Assert.Equal("Preventive", workOrder.WorkOrderType);
        Assert.NotNull(plan.NextDueDate); // طبق IntervalDays باید سررسید بعدی محاسبه شود
    }

    [Fact]
    public async Task PMGeneration_GenerateForPlan_ReturnsNull_WhenOpenWorkOrderAlreadyExists()
    {
        // طبق بند ۱۵/۵۵: محافظ Idempotency - اجرای دوباره نباید WorkOrder تکراری بسازد.
        var ctx = await SeedFullBaselineAsync(nameof(PMGeneration_GenerateForPlan_ReturnsNull_WhenOpenWorkOrderAlreadyExists));
        var equipment = new Equipment { TagNumber = "EQ-101", Name = "EQ-101", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "PM", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        var plan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-101", Title = "Monthly PM", TriggerType = "Calendar", IntervalDays = 30, IsActive = true
        };
        ctx.MaintenancePlans.Add(plan);
        await ctx.SaveChangesAsync();

        var service = new PMGenerationService(ctx, new FakeNumberingService());
        var first = await service.GenerateForPlanAsync(plan.MaintenancePlanId);
        var second = await service.GenerateForPlanAsync(plan.MaintenancePlanId);

        Assert.NotNull(first);
        Assert.Null(second); // تکراری تولید نشد
        Assert.Single(ctx.WorkOrders); // فقط یک WorkOrder وجود دارد
    }

    [Fact]
    public async Task PMGeneration_GenerateForPlan_GeneratesAgain_AfterPreviousWorkOrderClosed()
    {
        // طبق بند ۵۵: Closed/Cancelled/Rejected یعنی سررسید بعدی آزاد است.
        var ctx = await SeedFullBaselineAsync(nameof(PMGeneration_GenerateForPlan_GeneratesAgain_AfterPreviousWorkOrderClosed));
        var equipment = new Equipment { TagNumber = "EQ-102", Name = "EQ-102", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "PM", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        var plan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-102", Title = "Monthly PM", TriggerType = "Calendar", IntervalDays = 30, IsActive = true
        };
        ctx.MaintenancePlans.Add(plan);
        await ctx.SaveChangesAsync();

        var service = new PMGenerationService(ctx, new FakeNumberingService());
        var first = await service.GenerateForPlanAsync(plan.MaintenancePlanId);
        Assert.NotNull(first);

        var wo = ctx.WorkOrders.Single();
        wo.Status = "Closed";
        await ctx.SaveChangesAsync();

        var second = await service.GenerateForPlanAsync(plan.MaintenancePlanId);

        Assert.NotNull(second);
        Assert.Equal(2, ctx.WorkOrders.Count());
    }

    [Fact]
    public async Task PMGeneration_GenerateDueWorkOrders_OnlyGeneratesForDuePlans_AndSkipsOccupiedOnes()
    {
        var ctx = await SeedFullBaselineAsync(nameof(PMGeneration_GenerateDueWorkOrders_OnlyGeneratesForDuePlans_AndSkipsOccupiedOnes));
        var equipment = new Equipment { TagNumber = "EQ-103", Name = "EQ-103", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "PM", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();

        var duePlan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-103A", Title = "Due Plan", TriggerType = "Calendar", IntervalDays = 30, IsActive = true,
            NextDueDate = DateTime.Now.AddDays(-1)
        };
        var notDuePlan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-103B", Title = "Not Due Plan", TriggerType = "Calendar", IntervalDays = 30, IsActive = true,
            NextDueDate = DateTime.Now.AddDays(10)
        };
        var occupiedDuePlan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-103C", Title = "Occupied Due Plan", TriggerType = "Calendar", IntervalDays = 30, IsActive = true,
            NextDueDate = DateTime.Now.AddDays(-2)
        };
        ctx.MaintenancePlans.AddRange(duePlan, notDuePlan, occupiedDuePlan);
        await ctx.SaveChangesAsync();
        ctx.WorkOrders.Add(new WorkOrder
        {
            EquipmentId = equipment.EquipmentId, MaintenancePlanId = occupiedDuePlan.MaintenancePlanId,
            WorkOrderNumber = "WO-EXISTING", WorkOrderType = "Preventive", Status = "Open", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = new PMGenerationService(ctx, new FakeNumberingService());
        var result = await service.GenerateDueWorkOrdersAsync();

        Assert.Equal(1, result.WorkOrdersGenerated); // فقط duePlan
        Assert.Equal(1, result.PlansSkippedAlreadyHasOpenWorkOrder); // occupiedDuePlan
        Assert.Single(result.GeneratedWorkOrderNumbers);
    }

    // ================= RcmOperationalBridgeService =================

    [Fact]
    public async Task RcmBridge_ApplyDecision_Fails_WhenDecisionNotApproved()
    {
        var ctx = await SeedFullBaselineAsync(nameof(RcmBridge_ApplyDecision_Fails_WhenDecisionNotApproved));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();
        var equipment = new Equipment { TagNumber = "EQ-200", Name = "EQ-200", EquipmentType = "Pump", IsActive = true, EquipmentClassId = pumpCentClass.EquipmentClassId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, "rationale", "Operational");
        // عمداً تایید نمی‌شود (هنوز DRAFT)

        var bridge = new RcmOperationalBridgeService(ctx, new FakeNumberingService());
        var result = await bridge.ApplyApprovedDecisionAsync(decisionId, equipment.EquipmentId, "reliability_engineer1");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(ctx.MaintenanceStrategies); // هرگز خاموش ساخته نشد
    }

    [Fact]
    public async Task RcmBridge_ApplyDecision_Fails_WhenEquipmentClassMismatch()
    {
        var ctx = await SeedFullBaselineAsync(nameof(RcmBridge_ApplyDecision_Fails_WhenEquipmentClassMismatch));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var motorClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "MOTOR");
        var failureMode = await ctx.FailureModes.FirstAsync();
        var equipment = new Equipment { TagNumber = "EQ-201", Name = "EQ-201", EquipmentType = "Motor", IsActive = true, EquipmentClassId = motorClass.EquipmentClassId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, "rationale", "Operational");
        await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        var bridge = new RcmOperationalBridgeService(ctx, new FakeNumberingService());
        var result = await bridge.ApplyApprovedDecisionAsync(decisionId, equipment.EquipmentId, "reliability_engineer1");

        Assert.False(result.Success);
        Assert.Empty(ctx.MaintenanceStrategies);
    }

    [Fact]
    public async Task RcmBridge_ApplyDecision_CreatesStrategy_AndReuses_NeverDuplicates()
    {
        var ctx = await SeedFullBaselineAsync(nameof(RcmBridge_ApplyDecision_CreatesStrategy_AndReuses_NeverDuplicates));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();
        var equipment = new Equipment { TagNumber = "EQ-202", Name = "EQ-202", EquipmentType = "Pump", IsActive = true, EquipmentClassId = pumpCentClass.EquipmentClassId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, "rationale", "Operational");
        await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        var bridge = new RcmOperationalBridgeService(ctx, new FakeNumberingService());
        var first = await bridge.ApplyApprovedDecisionAsync(decisionId, equipment.EquipmentId, "reliability_engineer1");
        var second = await bridge.ApplyApprovedDecisionAsync(decisionId, equipment.EquipmentId, "reliability_engineer1");

        Assert.True(first.Success);
        Assert.False(first.MaintenanceStrategyWasReused);
        Assert.True(second.Success);
        Assert.True(second.MaintenanceStrategyWasReused);
        Assert.Equal(first.MaintenanceStrategyId, second.MaintenanceStrategyId);
        Assert.Single(ctx.MaintenanceStrategies); // هرگز تکراری ساخته نشد
    }

    [Fact]
    public async Task RcmBridge_ApplyDecision_CreatesMaintenancePlan_WhenPMTemplateRecommended()
    {
        var ctx = await SeedFullBaselineAsync(nameof(RcmBridge_ApplyDecision_CreatesMaintenancePlan_WhenPMTemplateRecommended));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();
        var pmTemplate = await ctx.PMTemplates.SingleAsync(x => x.Code == "PMT_PUMP_CENT_MONTHLY");
        var equipment = new Equipment { TagNumber = "EQ-203", Name = "EQ-203", EquipmentType = "Pump", IsActive = true, EquipmentClassId = pumpCentClass.EquipmentClassId };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);
        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREVENTIVE", null, pmTemplate.PMTemplateId, "rationale", "Operational");
        await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        var bridge = new RcmOperationalBridgeService(ctx, new FakeNumberingService());
        var first = await bridge.ApplyApprovedDecisionAsync(decisionId, equipment.EquipmentId, "reliability_engineer1");
        var second = await bridge.ApplyApprovedDecisionAsync(decisionId, equipment.EquipmentId, "reliability_engineer1");

        Assert.True(first.Success);
        Assert.NotNull(first.MaintenancePlanId);
        Assert.False(first.MaintenancePlanWasReused);
        Assert.True(second.MaintenancePlanWasReused);
        Assert.Single(ctx.MaintenancePlans);
        var plan = ctx.MaintenancePlans.Single();
        Assert.Equal(pmTemplate.TriggerType, plan.TriggerType);
        Assert.Equal(pmTemplate.IntervalDays, plan.IntervalDays);
    }

    // ================= WorkOrderReleaseGateService + Lifecycle wiring =================

    private static async Task<(Equipment Equipment, JobPlan JobPlan, MaintenanceStrategy Strategy, MaintenancePlan Plan, WorkOrder WorkOrder)> SeedGatedWorkOrderAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, string suffix, bool permitRequired, bool lotoRequired)
    {
        var equipment = new Equipment { TagNumber = $"EQ-G{suffix}", Name = $"EQ-G{suffix}", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();

        var jobPlan = new JobPlan { JobPlanNumber = $"JP-{suffix}", Title = "Overhaul", PermitRequired = permitRequired, LOTORequired = lotoRequired, IsActive = true };
        ctx.JobPlans.Add(jobPlan);
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "PM", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();

        var plan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = $"MP-{suffix}", Title = "Gated Plan", TriggerType = "Calendar", JobPlanId = jobPlan.JobPlanId, IsActive = true
        };
        ctx.MaintenancePlans.Add(plan);
        await ctx.SaveChangesAsync();

        var workOrder = new WorkOrder
        {
            EquipmentId = equipment.EquipmentId, MaintenancePlanId = plan.MaintenancePlanId,
            WorkOrderNumber = $"WO-{suffix}", WorkOrderType = "Preventive", Status = "Assigned", IsActive = true
        };
        ctx.WorkOrders.Add(workOrder);
        await ctx.SaveChangesAsync();

        return (equipment, jobPlan, strategy, plan, workOrder);
    }

    [Fact]
    public async Task ReleaseGate_CanRelease_WhenWorkOrderHasNoJobPlan()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_CanRelease_WhenWorkOrderHasNoJobPlan));
        var equipment = new Equipment { TagNumber = "EQ-G0", Name = "EQ-G0", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var workOrder = new WorkOrder { EquipmentId = equipment.EquipmentId, WorkOrderNumber = "WO-G0", WorkOrderType = "Corrective", Status = "Assigned", IsActive = true };
        ctx.WorkOrders.Add(workOrder);
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.True(result.CanRelease);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public async Task ReleaseGate_BlocksRelease_WhenPermitRequiredButNoActivePermit()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_BlocksRelease_WhenPermitRequiredButNoActivePermit));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "1", permitRequired: true, lotoRequired: false);

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.False(result.CanRelease);
        Assert.Contains(result.Reasons, r => r.Contains("Permit"));
    }

    [Fact]
    public async Task ReleaseGate_AllowsRelease_WhenPermitRequiredAndActivePermitLinked()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_AllowsRelease_WhenPermitRequiredAndActivePermitLinked));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "2", permitRequired: true, lotoRequired: false);
        ctx.Permits.Add(new Permit
        {
            PermitNumber = "PTW-1", PermitType = "HotWork", Title = "Hot Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.True(result.CanRelease);
    }

    [Fact]
    public async Task ReleaseGate_BlocksRelease_WhenLOTORequiredButIsolationNotVerified()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_BlocksRelease_WhenLOTORequiredButIsolationNotVerified));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "3", permitRequired: false, lotoRequired: true);
        var permit = new Permit
        {
            PermitNumber = "PTW-2", PermitType = "ColdWork", Title = "Cold Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        };
        ctx.Permits.Add(permit);
        await ctx.SaveChangesAsync();
        ctx.IsolationPoints.Add(new IsolationPoint { PermitId = permit.PermitId, EnergyType = "Electrical", Location = "MCC-1", Status = "Requested", IsActive = true });
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.False(result.CanRelease);
        Assert.Contains(result.Reasons, r => r.Contains("LOTO"));
    }

    [Fact]
    public async Task ReleaseGate_AllowsRelease_WhenLOTORequiredAndIsolationVerified()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_AllowsRelease_WhenLOTORequiredAndIsolationVerified));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "4", permitRequired: false, lotoRequired: true);
        var permit = new Permit
        {
            PermitNumber = "PTW-3", PermitType = "ColdWork", Title = "Cold Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        };
        ctx.Permits.Add(permit);
        await ctx.SaveChangesAsync();
        ctx.IsolationPoints.Add(new IsolationPoint { PermitId = permit.PermitId, EnergyType = "Electrical", Location = "MCC-1", Status = "Verified", IsActive = true });
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.True(result.CanRelease);
    }

    [Fact]
    public async Task ReleaseGate_BlocksRelease_WhenGasTestRequiredButMissing()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_BlocksRelease_WhenGasTestRequiredButMissing));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "5", permitRequired: true, lotoRequired: false);
        var permit = new Permit
        {
            PermitNumber = "PTW-4", PermitType = "HotWork", Title = "Hot Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        };
        ctx.Permits.Add(permit);
        ctx.PermitExecutionRequirements.Add(new PermitExecutionRequirement { PermitType = "HotWork", RequireValidGasTest = true, IsActive = true });
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.False(result.CanRelease);
        Assert.Contains(result.Reasons, r => r.Contains("Gas Test"));
    }

    [Fact]
    public async Task ReleaseGate_AllowsRelease_WhenGasTestRequiredAndValidAndNotExpired()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_AllowsRelease_WhenGasTestRequiredAndValidAndNotExpired));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "6", permitRequired: true, lotoRequired: false);
        var permit = new Permit
        {
            PermitNumber = "PTW-5", PermitType = "HotWork", Title = "Hot Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        };
        ctx.Permits.Add(permit);
        ctx.PermitExecutionRequirements.Add(new PermitExecutionRequirement { PermitType = "HotWork", RequireValidGasTest = true, IsActive = true });
        await ctx.SaveChangesAsync();
        ctx.GasTests.Add(new GasTest
        {
            PermitId = permit.PermitId, Location = "Near Pump", TestedAt = DateTime.Now, TesterEmployeeId = 1,
            Status = "Passed", ValidUntil = DateTime.Now.AddHours(4), IsActive = true
        });
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.True(result.CanRelease);
    }

    [Fact]
    public async Task ReleaseGate_BlocksRelease_WhenGasTestExpired()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReleaseGate_BlocksRelease_WhenGasTestExpired));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "7", permitRequired: true, lotoRequired: false);
        var permit = new Permit
        {
            PermitNumber = "PTW-6", PermitType = "HotWork", Title = "Hot Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        };
        ctx.Permits.Add(permit);
        ctx.PermitExecutionRequirements.Add(new PermitExecutionRequirement { PermitType = "HotWork", RequireValidGasTest = true, IsActive = true });
        await ctx.SaveChangesAsync();
        ctx.GasTests.Add(new GasTest
        {
            PermitId = permit.PermitId, Location = "Near Pump", TestedAt = DateTime.Now.AddHours(-6), TesterEmployeeId = 1,
            Status = "Passed", ValidUntil = DateTime.Now.AddHours(-2), IsActive = true
        });
        await ctx.SaveChangesAsync();

        var gate = new WorkOrderReleaseGateService(ctx);
        var result = await gate.EvaluateAsync(workOrder.WorkOrderId);

        Assert.False(result.CanRelease);
    }

    [Fact]
    public async Task Lifecycle_AdvanceToInProgress_Fails_WhenReleaseGateBlocks()
    {
        // طبق بند ۱۸: تنها نقطه‌ی اجرای گیت، خودِ AdvanceStatusAsync است - نه یک مسیر Controller
        // قابل‌دورزدن.
        var ctx = await SeedFullBaselineAsync(nameof(Lifecycle_AdvanceToInProgress_Fails_WhenReleaseGateBlocks));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "8", permitRequired: true, lotoRequired: false);

        var lifecycle = new WorkOrderLifecycleService(ctx, new WorkOrderReleaseGateService(ctx));
        var (success, error) = await lifecycle.AdvanceStatusAsync(
            workOrder.WorkOrderId, "InProgress", null, null, null, null,
            null, null, null, null);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("Assigned", ctx.WorkOrders.Single().Status); // وضعیت تغییر نکرد
    }

    [Fact]
    public async Task Lifecycle_AdvanceToInProgress_Succeeds_WhenReleaseGatePasses()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Lifecycle_AdvanceToInProgress_Succeeds_WhenReleaseGatePasses));
        var (_, _, _, _, workOrder) = await SeedGatedWorkOrderAsync(ctx, "9", permitRequired: true, lotoRequired: false);
        ctx.Permits.Add(new Permit
        {
            PermitNumber = "PTW-7", PermitType = "HotWork", Title = "Hot Work", WorkOrderId = workOrder.WorkOrderId,
            RequestedByEmployeeId = 1, Status = "Active", IsActive = true
        });
        await ctx.SaveChangesAsync();

        var lifecycle = new WorkOrderLifecycleService(ctx, new WorkOrderReleaseGateService(ctx));
        var (success, error) = await lifecycle.AdvanceStatusAsync(
            workOrder.WorkOrderId, "InProgress", null, null, null, null,
            null, null, null, null);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("InProgress", ctx.WorkOrders.Single().Status);
    }

    [Fact]
    public async Task Lifecycle_AdvanceToAssigned_IsNotAffectedByReleaseGate()
    {
        // گیت فقط روی گذار به InProgress اعمال می‌شود، نه روی سایر گذارها.
        var ctx = await SeedFullBaselineAsync(nameof(Lifecycle_AdvanceToAssigned_IsNotAffectedByReleaseGate));
        var equipment = new Equipment { TagNumber = "EQ-G10", Name = "EQ-G10", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        var jobPlan = new JobPlan { JobPlanNumber = "JP-10", Title = "Overhaul", PermitRequired = true, IsActive = true };
        ctx.JobPlans.Add(jobPlan);
        var strategy = new MaintenanceStrategy { EquipmentId = equipment.EquipmentId, StrategyType = "PM", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        var plan = new MaintenancePlan
        {
            EquipmentId = equipment.EquipmentId, MaintenanceStrategyId = strategy.MaintenanceStrategyId,
            PlanNumber = "MP-10", Title = "Gated Plan", TriggerType = "Calendar", JobPlanId = jobPlan.JobPlanId, IsActive = true
        };
        ctx.MaintenancePlans.Add(plan);
        await ctx.SaveChangesAsync();
        var workOrder = new WorkOrder { EquipmentId = equipment.EquipmentId, MaintenancePlanId = plan.MaintenancePlanId, WorkOrderNumber = "WO-10", WorkOrderType = "Preventive", Status = "Open", IsActive = true };
        ctx.WorkOrders.Add(workOrder);
        await ctx.SaveChangesAsync();

        var lifecycle = new WorkOrderLifecycleService(ctx, new WorkOrderReleaseGateService(ctx));
        var (success, error) = await lifecycle.AdvanceStatusAsync(
            workOrder.WorkOrderId, "Assigned", null, null, null, null,
            null, null, null, null);

        Assert.True(success); // هیچ Permit ای موجود نیست ولی گذار به Assigned مانعی ندارد
        Assert.Equal("Assigned", ctx.WorkOrders.Single().Status);
    }
}
