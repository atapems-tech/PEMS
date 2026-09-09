using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence» بند ۴۹: تست‌های چرخه‌عمر
/// (DRAFT→UNDER_REVIEW→APPROVED→SUPERSEDED)، RPN محاسبه‌شده، آماده‌بودی (Readiness)، و ایمنی
/// داده (هرگز یک تحلیل تاییدشده مستقیم ویرایش نمی‌شود).
/// </summary>
public class RcmFmeaTests
{
    private static async Task<PEMS.Persistence.Context.PEMSDbContext> SeedFullBaselineAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase4_{dbName}");
        var services = BuildProvider(ctx);

        await DisciplineSeeder.SeedAsync(services);
        await MaintenanceGroupSeeder.SeedAsync(services);
        await MaintenanceTypeSeeder.SeedAsync(services);
        await MaintenanceStrategyTypeSeeder.SeedAsync(services);
        await EquipmentClassSeeder.SeedAsync(services);
        await StandardReferenceSeeder.SeedAsync(services);
        await MaintenanceActivitySeeder.SeedAsync(services);

        await FailureCauseSeeder.SeedAsync(services);
        await FailureMechanismSeeder.SeedAsync(services);
        await FailureEffectSeeder.SeedAsync(services);
        await FailureConsequenceSeeder.SeedAsync(services);
        await FailureCauseMechanismSeeder.SeedAsync(services);
        await FailureModeSeeder.SeedAsync(services);
        await PMTemplateSeeder.SeedAsync(services);

        return ctx;
    }

    private static IServiceProvider BuildProvider(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        return services.BuildServiceProvider();
    }

    // ================= FMEA Lifecycle =================

    [Fact]
    public async Task FMEA_CreateDraft_StartsAtRevisionOneWithDraftStatus()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_CreateDraft_StartsAtRevisionOneWithDraftStatus));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        var service = new FMEAService(ctx);
        var id = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Centrifugal Pump FMEA", null, "engineer1");

        var dto = await service.GetByIdAsync(id);
        Assert.NotNull(dto);
        Assert.Equal(1, dto!.Revision);
        Assert.Equal("DRAFT", dto.Status);
        Assert.Equal("engineer1", dto.PreparedBy);
    }

    [Fact]
    public async Task FMEA_AddItem_ComputesRiskPriorityNumber_WhenAllThreeScoresProvided()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_AddItem_ComputesRiskPriorityNumber_WhenAllThreeScoresProvided));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();

        var service = new FMEAService(ctx);
        var analysisId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Test FMEA", null, "engineer1");

        await service.AddItemAsync(analysisId, failureMode.FailureModeId, null, null, null, null, null,
            "Vibration monitoring", "Routine inspection", "Increase inspection frequency",
            severity: 8, occurrence: 4, detectability: 3, maintenanceStrategyTypeId: null, standardReferenceId: null);

        var dto = await service.GetByIdAsync(analysisId);
        var item = dto!.Items.Single();
        Assert.Equal(8 * 4 * 3, item.RiskPriorityNumber);
    }

    [Fact]
    public async Task FMEA_AddItem_WithoutFullScores_RiskPriorityNumberIsNull_NeverFabricated()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_AddItem_WithoutFullScores_RiskPriorityNumberIsNull_NeverFabricated));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();

        var service = new FMEAService(ctx);
        var analysisId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Test FMEA", null, "engineer1");

        // طبق بند ۱۵/۳۰: فقط Severity وارد شده - RPN هرگز نباید حدس زده شود
        await service.AddItemAsync(analysisId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, severity: 8, occurrence: null, detectability: null, maintenanceStrategyTypeId: null, standardReferenceId: null);

        var dto = await service.GetByIdAsync(analysisId);
        Assert.Null(dto!.Items.Single().RiskPriorityNumber);
    }

    [Fact]
    public async Task FMEA_CannotAddItem_ToApprovedAnalysis()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_CannotAddItem_ToApprovedAnalysis));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();

        var service = new FMEAService(ctx);
        var analysisId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Test FMEA", null, "engineer1");
        await service.SubmitForReviewAsync(analysisId, "reviewer1");
        await service.ApproveAsync(analysisId, "manager1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddItemAsync(analysisId, failureMode.FailureModeId, null, null, null, null, null,
                null, null, null, null, null, null, null, null));
    }

    [Fact]
    public async Task FMEA_ApprovingNewRevision_SupersedesPreviousApprovedRevision()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_ApprovingNewRevision_SupersedesPreviousApprovedRevision));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        var service = new FMEAService(ctx);
        var firstId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Rev 1", null, "engineer1");
        await service.SubmitForReviewAsync(firstId, "reviewer1");
        await service.ApproveAsync(firstId, "manager1");

        var secondId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Rev 2", null, "engineer1");
        await service.SubmitForReviewAsync(secondId, "reviewer1");
        await service.ApproveAsync(secondId, "manager1");

        var first = await service.GetByIdAsync(firstId);
        var second = await service.GetByIdAsync(secondId);

        Assert.Equal("SUPERSEDED", first!.Status); // هرگز حذف نمی‌شود، فقط علامت‌گذاری
        Assert.Equal("APPROVED", second!.Status);
        Assert.Equal(2, second.Revision);
    }

    [Fact]
    public async Task FMEA_SubmitForReview_OnlyWorksFromDraft()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_SubmitForReview_OnlyWorksFromDraft));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        var service = new FMEAService(ctx);
        var analysisId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Test", null, "engineer1");
        await service.SubmitForReviewAsync(analysisId, "reviewer1");

        var secondAttempt = await service.SubmitForReviewAsync(analysisId, "reviewer1");
        Assert.False(secondAttempt); // از UNDER_REVIEW نمی‌توان دوباره Submit کرد
    }

    [Fact]
    public async Task FMEA_Reject_ReturnsAnalysisToDraft()
    {
        var ctx = await SeedFullBaselineAsync(nameof(FMEA_Reject_ReturnsAnalysisToDraft));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        var service = new FMEAService(ctx);
        var analysisId = await service.CreateDraftAsync(pumpCentClass.EquipmentClassId, "Test", null, "engineer1");
        await service.SubmitForReviewAsync(analysisId, "reviewer1");
        var rejected = await service.RejectAsync(analysisId, "reviewer1");

        Assert.True(rejected);
        var dto = await service.GetByIdAsync(analysisId);
        Assert.Equal("DRAFT", dto!.Status);
    }

    // ================= RCM Lifecycle =================

    [Fact]
    public async Task RCM_CreateDraft_LinksToFMEAAnalysis()
    {
        var ctx = await SeedFullBaselineAsync(nameof(RCM_CreateDraft_LinksToFMEAAnalysis));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");

        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");

        var dto = await rcmService.GetByIdAsync(rcmId);
        Assert.NotNull(dto);
        Assert.Equal(fmeaId, dto!.FMEAAnalysisId);
        Assert.Equal("DRAFT", dto.Status);
    }

    [Fact]
    public async Task RCM_AddDecision_NeverCreatesRealMaintenanceStrategy()
    {
        // طبق بند ۱۸/۳۱: RCMDecision یک توصیه‌ی سطح کلاس است - هرگز مستقیم یک MaintenanceStrategy
        // واقعی (سطح Equipment) نمی‌سازد.
        var ctx = await SeedFullBaselineAsync(nameof(RCM_AddDecision_NeverCreatesRealMaintenanceStrategy));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();
        var strategyType = await ctx.MaintenanceStrategyTypes.FirstAsync(x => x.Code == "PDM");

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");
        await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", strategyType.MaintenanceStrategyTypeId, null,
            "Vibration analysis is technically feasible and cost-effective", "Operational");

        Assert.Equal(0, await ctx.MaintenanceStrategies.CountAsync()); // هیچ رکورد واقعی ساخته نشد
        Assert.Equal(1, await ctx.RCMDecisions.CountAsync());
    }

    [Fact]
    public async Task RCM_ApproveDecision_RequiresDraftStatus()
    {
        var ctx = await SeedFullBaselineAsync(nameof(RCM_ApproveDecision_RequiresDraftStatus));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var failureMode = await ctx.FailureModes.FirstAsync();

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        var itemId = await fmeaService.AddItemAsync(fmeaId, failureMode.FailureModeId, null, null, null, null, null,
            null, null, null, null, null, null, null, null);

        var rcmService = new RCMService(ctx);
        var rcmId = await rcmService.CreateDraftAsync(pumpCentClass.EquipmentClassId, fmeaId, "engineer1");
        var decisionId = await rcmService.AddDecisionAsync(rcmId, itemId, "PREDICTIVE", null, null, null, "Operational");

        var firstApproval = await rcmService.ApproveDecisionAsync(decisionId, "manager1");
        var secondApproval = await rcmService.ApproveDecisionAsync(decisionId, "manager1");

        Assert.True(firstApproval);
        Assert.False(secondApproval); // نمی‌توان دو بار تایید کرد
    }

    // ================= Readiness =================

    [Fact]
    public async Task Readiness_ClassWithNoEquipment_IsNotApplicable()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Readiness_ClassWithNoEquipment_IsNotApplicable));

        var service = new RcmFmeaReadinessService(ctx);
        var batteryClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "BATTERY");
        var readiness = await service.GetForEquipmentClassAsync(batteryClass.EquipmentClassId);

        Assert.NotNull(readiness);
        Assert.Equal(0, readiness!.EquipmentCount);
        Assert.Equal("NOT_APPLICABLE", readiness.FMEAReadiness);
        Assert.Equal("NOT_APPLICABLE", readiness.RCMReadiness);
    }

    [Fact]
    public async Task Readiness_ClassWithEquipmentButNoFailureModes_IsInsufficientData()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Readiness_ClassWithEquipmentButNoFailureModes_IsInsufficientData));

        // TRANSFORMER در خط‌مبنای فاز ۱-۳.۵ هیچ FailureMode مستقیمی ندارد (FailureModeSeeder
        // فاز ۲ فقط Pump/Motor/Valve/Compressor/Fan/Gearbox/HeatExchanger/PressureVessel/
        // StorageTank/Instrumentation را پوشش داد - نه Transformer/Switchgear)
        var transformerClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "TRANSFORMER");
        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "transformer",
            EquipmentClassId = transformerClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new RcmFmeaReadinessService(ctx);
        var readiness = await service.GetForEquipmentClassAsync(transformerClass.EquipmentClassId);

        Assert.NotNull(readiness);
        Assert.Equal(1, readiness!.EquipmentCount);
        Assert.Equal(0, readiness.FailureModeCount);
        Assert.Equal("INSUFFICIENT_DATA", readiness.FMEAReadiness);
    }

    [Fact]
    public async Task Readiness_RCM_RequiresApprovedFMEA_BeforeBecomingReady()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Readiness_RCM_RequiresApprovedFMEA_BeforeBecomingReady));
        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var readinessService = new RcmFmeaReadinessService(ctx);
        var before = await readinessService.GetForEquipmentClassAsync(pumpCentClass.EquipmentClassId);
        Assert.Equal("INSUFFICIENT_DATA", before!.RCMReadiness); // هنوز FMEA تاییدشده‌ای نیست

        var fmeaService = new FMEAService(ctx);
        var fmeaId = await fmeaService.CreateDraftAsync(pumpCentClass.EquipmentClassId, "FMEA", null, "engineer1");
        await fmeaService.SubmitForReviewAsync(fmeaId, "reviewer1");
        await fmeaService.ApproveAsync(fmeaId, "manager1");

        var after = await readinessService.GetForEquipmentClassAsync(pumpCentClass.EquipmentClassId);
        Assert.NotEqual("INSUFFICIENT_DATA", after!.RCMReadiness); // حالا حداقل PARTIALLY_READY
    }

    [Fact]
    public async Task CoverageMatrix_NeverMassGeneratesAnalyses_OnlyReadsExisting()
    {
        // طبق بند ۵۵: هیچ‌جا نباید خودکار FMEA/RCM بسازد - فقط می‌خواند
        var ctx = await SeedFullBaselineAsync(nameof(CoverageMatrix_NeverMassGeneratesAnalyses_OnlyReadsExisting));

        var service = new RcmFmeaReadinessService(ctx);
        await service.GetCoverageMatrixAsync();

        Assert.Equal(0, await ctx.FMEAAnalyses.CountAsync());
        Assert.Equal(0, await ctx.RCMAnalyses.CountAsync());
    }
}
