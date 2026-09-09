using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification + ISO 14224 Failure Taxonomy + PM/Job Plan
/// Intelligence» بند ۴۳: تست‌های Seed (خط‌مبنای موجود، Idempotent)، طبقه‌بندی (قطعی، بدون حدس،
/// هرگز بازنویسی نمی‌کند)، تاکسونومی خرابی (روابط معتبر)، PM Template (JobPlanId=Null در
/// خط‌مبنا)، و Maintenance Intelligence Service (تجمیع بدون N+1).
/// </summary>
public class EquipmentClassificationAndFailureTaxonomyTests
{
    private static async Task<PEMS.Persistence.Context.PEMSDbContext> SeedFullBaselineAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase2_{dbName}");
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

    // ================= Equipment Class Hierarchy =================

    [Fact]
    public async Task Seed_EquipmentClass_Phase1CodesUnchanged_NowHaveParent()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_EquipmentClass_Phase1CodesUnchanged_NowHaveParent));

        var pumpCent = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        Assert.NotNull(pumpCent.ParentEquipmentClassId);

        var parent = await ctx.EquipmentClasses.SingleAsync(x => x.EquipmentClassId == pumpCent.ParentEquipmentClassId);
        Assert.Equal("PUMP", parent.Code);
    }

    [Fact]
    public async Task Seed_EquipmentClass_NewParentCategoriesExist()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_EquipmentClass_NewParentCategoriesExist));

        Assert.True(await ctx.EquipmentClasses.AnyAsync(x => x.Code == "ROTATING_EQUIPMENT" && x.ParentEquipmentClassId == null));
        Assert.True(await ctx.EquipmentClasses.AnyAsync(x => x.Code == "INSTRUMENTATION_EQUIPMENT" && x.ParentEquipmentClassId == null));
    }

    [Fact]
    public async Task Seed_EquipmentClass_RunningTwice_IsIdempotent_NeverOverwritesManualParent()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_EquipmentClass_RunningTwice_IsIdempotent_NeverOverwritesManualParent));
        var services = BuildProvider(ctx);

        var pumpCent = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var staticEquipment = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "STATIC_EQUIPMENT");

        // شبیه‌سازی یک انتساب دستی کاربر - باید حفظ شود
        pumpCent.ParentEquipmentClassId = staticEquipment.EquipmentClassId;
        await ctx.SaveChangesAsync();

        await EquipmentClassSeeder.SeedAsync(services);

        var reloaded = await ctx.EquipmentClasses.AsNoTracking().SingleAsync(x => x.Code == "PUMP_CENT");
        Assert.Equal(staticEquipment.EquipmentClassId, reloaded.ParentEquipmentClassId);

        var countBefore = await ctx.EquipmentClasses.CountAsync();
        await EquipmentClassSeeder.SeedAsync(services);
        Assert.Equal(countBefore, await ctx.EquipmentClasses.CountAsync());
    }

    // ================= Failure Taxonomy =================

    [Fact]
    public async Task Seed_FailureTaxonomy_DefaultsExist()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_FailureTaxonomy_DefaultsExist));

        Assert.True(await ctx.FailureCauses.AnyAsync(x => x.Code == "CORROSION"));
        Assert.True(await ctx.FailureMechanisms.AnyAsync(x => x.Code == "PITTING"));
        Assert.True(await ctx.FailureEffects.AnyAsync(x => x.Code == "LOSS_OF_FUNCTION"));
        Assert.True(await ctx.FailureConsequences.AnyAsync(x => x.Code == "SAFETY"));
        Assert.True(await ctx.FailureModes.AnyAsync(x => x.Code == "PUMP_SEAL_LEAKAGE"));
    }

    [Fact]
    public async Task Seed_FailureTaxonomy_ModeHasValidEquipmentClassAndEffectLinks()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_FailureTaxonomy_ModeHasValidEquipmentClassAndEffectLinks));

        var mode = await ctx.FailureModes
            .Include(x => x.EquipmentClass)
            .Include(x => x.FailureEffect)
            .Include(x => x.FailureConsequence)
            .SingleAsync(x => x.Code == "PUMP_SEAL_LEAKAGE");

        Assert.Equal("PUMP", mode.EquipmentClass!.Code);
        Assert.Equal("LEAKAGE", mode.FailureEffect!.Code);
        Assert.Equal("ENVIRONMENT", mode.FailureConsequence!.Code);
    }

    [Fact]
    public async Task Seed_FailureTaxonomy_ModeCauseLinks_AreNotEmpty()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_FailureTaxonomy_ModeCauseLinks_AreNotEmpty));

        var mode = await ctx.FailureModes.SingleAsync(x => x.Code == "PUMP_BEARING_FAILURE");
        var causeLinks = await ctx.FailureModeCauses.Where(x => x.FailureModeId == mode.FailureModeId).ToListAsync();

        Assert.True(causeLinks.Count >= 2);
    }

    [Fact]
    public async Task Seed_FailureTaxonomy_CauseMechanismLinks_Exist()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_FailureTaxonomy_CauseMechanismLinks_Exist));

        var corrosion = await ctx.FailureCauses.SingleAsync(x => x.Code == "CORROSION");
        var links = await ctx.FailureCauseMechanisms.Where(x => x.FailureCauseId == corrosion.FailureCauseId).ToListAsync();

        Assert.True(links.Count >= 2);
    }

    [Fact]
    public async Task Seed_FailureTaxonomy_DuplicateCodeRejectedByUniqueBusinessRule()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_FailureTaxonomy_DuplicateCodeRejectedByUniqueBusinessRule));
        var services = BuildProvider(ctx);

        var countBefore = await ctx.FailureCauses.CountAsync();
        await FailureCauseSeeder.SeedAsync(services); // اجرای دوباره - نباید ردیف تکراری اضافه کند
        Assert.Equal(countBefore, await ctx.FailureCauses.CountAsync());
    }

    // ================= PM Template =================

    [Fact]
    public async Task Seed_PMTemplate_DefaultsExist_WithNullJobPlanId()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_PMTemplate_DefaultsExist_WithNullJobPlanId));

        var template = await ctx.PMTemplates.SingleAsync(x => x.Code == "PMT_PUMP_CENT_MONTHLY");
        Assert.Null(template.JobPlanId); // طبق بند ۴۴: هرگز یک Job Plan فرضی/ساختگی متصل نمی‌شود
        Assert.Equal("Calendar", template.TriggerType);
        Assert.Equal(30, template.IntervalDays);
    }

    [Fact]
    public async Task Seed_PMTemplate_IsIdempotent()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Seed_PMTemplate_IsIdempotent));
        var services = BuildProvider(ctx);

        var countBefore = await ctx.PMTemplates.CountAsync();
        await PMTemplateSeeder.SeedAsync(services);
        Assert.Equal(countBefore, await ctx.PMTemplates.CountAsync());
    }

    // ================= Equipment Classification Backfill =================

    [Fact]
    public async Task Backfill_ExactMatchType_ClassifiesEquipment_WithExactConfidenceAndRuleSource()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_ExactMatchType_ClassifiesEquipment_WithExactConfidenceAndRuleSource));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "compressor",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(1, result.AutoClassifiedCount);
        Assert.Equal(0, result.SuggestionsCreatedCount);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.NotNull(equipment.EquipmentClassId);
        Assert.Equal("EXACT", equipment.ClassificationConfidence);
        Assert.Equal("RULE", equipment.ClassificationSource);

        var equipmentClass = await ctx.EquipmentClasses.SingleAsync(x => x.EquipmentClassId == equipment.EquipmentClassId);
        Assert.Equal("COMPRESSOR", equipmentClass.Code);
    }

    [Fact]
    public async Task Backfill_HighConfidenceTypeNameCombo_ClassifiesEquipment_WithHighConfidence()
    {
        // طبق فاز ۳ بند ۸: کشف اصلی این فاز - EquipmentType='DEVICE' به‌تنهایی عمومی است، ولی
        // ترکیب با Name واقعی ("THERMOWELL") قطعی و بدون‌ابهام یک کلاس مشخص را تعیین می‌کند.
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_HighConfidenceTypeNameCombo_ClassifiesEquipment_WithHighConfidence));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-TW-480210", Name = "THERMOWELL", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(1, result.AutoClassifiedCount);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Equal("HIGH", equipment.ClassificationConfidence);
        Assert.Equal("RULE", equipment.ClassificationSource);

        var equipmentClass = await ctx.EquipmentClasses.SingleAsync(x => x.EquipmentClassId == equipment.EquipmentClassId);
        Assert.Equal("THERMOWELL", equipmentClass.Code);
    }

    [Fact]
    public async Task Backfill_AmbiguousType_LeavesEquipmentClassIdNull_NeverGuesses()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_AmbiguousType_LeavesEquipmentClassIdNull_NeverGuesses));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(0, result.AutoClassifiedCount);
        Assert.Equal(0, result.SuggestionsCreatedCount);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Null(equipment.EquipmentClassId);
    }

    [Fact]
    public async Task Backfill_AlreadyClassifiedEquipment_IsNeverOverwritten()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_AlreadyClassifiedEquipment_IsNeverOverwritten));

        var manualClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "TURBINE");

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "compressor",
            EquipmentClassId = manualClass.EquipmentClassId, ClassificationSource = "MANUAL", ClassificationConfidence = "HIGH",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(0, result.AutoClassifiedCount); // چون EquipmentClassId از قبل مقداردهی شده - Backfill کاری نمی‌کند

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Equal(manualClass.EquipmentClassId, equipment.EquipmentClassId);
        Assert.Equal("MANUAL", equipment.ClassificationSource);
    }

    [Fact]
    public async Task Backfill_IsIdempotent_RunningTwiceDoesNotDoubleApply()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_IsIdempotent_RunningTwiceDoesNotDoubleApply));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var firstRun = await service.BackfillAsync();
        var secondRun = await service.BackfillAsync();

        Assert.Equal(1, firstRun.AutoClassifiedCount);
        Assert.Equal(0, secondRun.AutoClassifiedCount);
    }

    [Fact]
    public async Task Backfill_MediumConfidence_CreatesSuggestion_NeverWritesEquipmentClassId()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_MediumConfidence_CreatesSuggestion_NeverWritesEquipmentClassId));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-XA-100", Name = "CUMULATIVE ALARM", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(0, result.AutoClassifiedCount);
        Assert.Equal(1, result.SuggestionsCreatedCount);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Null(equipment.EquipmentClassId); // MEDIUM هرگز مستقیم روی Equipment نوشته نمی‌شود

        var suggestion = await ctx.EquipmentClassificationSuggestions.SingleAsync(x => x.EquipmentId == 1);
        Assert.Equal("MEDIUM", suggestion.Confidence);
        Assert.Equal("PENDING", suggestion.Status);
    }

    [Fact]
    public async Task Backfill_MediumConfidence_IsIdempotent_NeverDuplicatesSuggestion()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_MediumConfidence_IsIdempotent_NeverDuplicatesSuggestion));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-XA-100", Name = "CUMULATIVE ALARM", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();
        await service.BackfillAsync();

        Assert.Equal(1, await ctx.EquipmentClassificationSuggestions.CountAsync(x => x.EquipmentId == 1));
    }

    [Fact]
    public async Task Backfill_MediumConfidence_RejectedSuggestion_IsNeverRecreated()
    {
        // طبق بند ۱۳: یک تصمیم قبلی انسانی (Rejected) هرگز توسط اجرای بعدی Backfill دور زده نمی‌شود.
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_MediumConfidence_RejectedSuggestion_IsNeverRecreated));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-XA-100", Name = "CUMULATIVE ALARM", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();

        var suggestion = await ctx.EquipmentClassificationSuggestions.SingleAsync(x => x.EquipmentId == 1);
        var rejected = await service.RejectSuggestionAsync(suggestion.EquipmentClassificationSuggestionId, "reviewer", "not applicable");
        Assert.True(rejected);

        await service.BackfillAsync();

        Assert.Equal(1, await ctx.EquipmentClassificationSuggestions.CountAsync(x => x.EquipmentId == 1));
        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Null(equipment.EquipmentClassId);
    }

    [Fact]
    public async Task AcceptSuggestion_AppliesClassification_MarksReviewedByAndAt()
    {
        var ctx = await SeedFullBaselineAsync(nameof(AcceptSuggestion_AppliesClassification_MarksReviewedByAndAt));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-XA-100", Name = "CUMULATIVE ALARM", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();
        var suggestion = await ctx.EquipmentClassificationSuggestions.SingleAsync(x => x.EquipmentId == 1);

        var accepted = await service.AcceptSuggestionAsync(suggestion.EquipmentClassificationSuggestionId, "reviewer1", "looks right");
        Assert.True(accepted);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.NotNull(equipment.EquipmentClassId);
        Assert.Equal("MEDIUM", equipment.ClassificationConfidence);
        Assert.Equal("MANUAL", equipment.ClassificationSource); // تایید انسانی - نه RULE خام

        var reloadedSuggestion = await ctx.EquipmentClassificationSuggestions.AsNoTracking()
            .SingleAsync(x => x.EquipmentClassificationSuggestionId == suggestion.EquipmentClassificationSuggestionId);
        Assert.Equal("ACCEPTED", reloadedSuggestion.Status);
        Assert.Equal("reviewer1", reloadedSuggestion.ReviewedBy);
        Assert.NotNull(reloadedSuggestion.ReviewedAt);
    }

    [Fact]
    public async Task AcceptSuggestion_AlreadyReviewed_ReturnsFalse_DoesNotDoubleApply()
    {
        var ctx = await SeedFullBaselineAsync(nameof(AcceptSuggestion_AlreadyReviewed_ReturnsFalse_DoesNotDoubleApply));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-XA-100", Name = "CUMULATIVE ALARM", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();
        var suggestion = await ctx.EquipmentClassificationSuggestions.SingleAsync(x => x.EquipmentId == 1);

        await service.RejectSuggestionAsync(suggestion.EquipmentClassificationSuggestionId, "reviewer1", null);
        var secondAttempt = await service.AcceptSuggestionAsync(suggestion.EquipmentClassificationSuggestionId, "reviewer2", null);

        Assert.False(secondAttempt);
    }

    [Fact]
    public async Task Dashboard_ReportsCorrectCoverageAndBreakdowns()
    {
        var ctx = await SeedFullBaselineAsync(nameof(Dashboard_ReportsCorrectCoverageAndBreakdowns));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "compressor",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 2, TagNumber = "80-PSH-1", Name = "PANEL", EquipmentType = "PANEL",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(2, dashboard.TotalActiveEquipment);
        Assert.Equal(1, dashboard.ClassifiedCount);
        Assert.Equal(1, dashboard.UnclassifiedCount);
        Assert.Equal(50.0, dashboard.CoveragePercent);
        Assert.Contains(dashboard.ByConfidence, x => x.Key == "EXACT" && x.Count == 1);
        Assert.Contains(dashboard.TopUnclassifiedEquipmentTypes, x => x.Key == "PANEL" && x.Count == 1);
    }

    [Fact]
    public async Task ReviewQueue_ReturnsOnlyRequestedStatus_Paginated()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ReviewQueue_ReturnsOnlyRequestedStatus_Paginated));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "80-XA-100", Name = "CUMULATIVE ALARM", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();

        var pendingPage = await service.GetReviewQueueAsync(1, 50, "PENDING");
        Assert.Equal(1, pendingPage.TotalCount);
        Assert.Equal("80-XA-100", pendingPage.Items.Single().TagNumber);
        Assert.Equal("CUMULATIVE ALARM", pendingPage.Items.Single().EquipmentName);

        var acceptedPage = await service.GetReviewQueueAsync(1, 50, "ACCEPTED");
        Assert.Equal(0, acceptedPage.TotalCount);
    }

    // ================= Maintenance Intelligence Service =================

    [Fact]
    public async Task IntelligenceService_ClassifiedEquipment_ReturnsApplicableActivitiesAndPMTemplates()
    {
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_ClassifiedEquipment_ReturnsApplicableActivitiesAndPMTemplates));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(1);

        Assert.NotNull(result);
        Assert.Equal("PUMP_CENT", result!.EquipmentClassCode);
        Assert.NotEmpty(result.ApplicableActivities); // طبق فاز ۱: Vibration Measurement روی PUMP_CENT کاربردپذیر است
        Assert.Contains(result.CandidatePMTemplates, t => t.Code == "PMT_PUMP_CENT_MONTHLY");
    }

    [Fact]
    public async Task IntelligenceService_UnclassifiedEquipment_ReturnsEmptyRecommendations_NoGuessing()
    {
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_UnclassifiedEquipment_ReturnsEmptyRecommendations_NoGuessing));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(1);

        Assert.NotNull(result);
        Assert.Null(result!.EquipmentClassId);
        Assert.Empty(result.ApplicableActivities);
        Assert.Empty(result.CandidatePMTemplates);
    }

    [Fact]
    public async Task IntelligenceService_NonExistentEquipment_ReturnsNull()
    {
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_NonExistentEquipment_ReturnsNull));

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(999);

        Assert.Null(result);
    }

    // ================= Hierarchy-Aware Intelligence (Phase 3 بند ۱۷-۲۱) =================

    [Fact]
    public async Task IntelligenceService_Activity_InheritedFromParentClass_WhenNotDefinedOnExactClass()
    {
        // PUMP_CENT فرزند PUMP است (طبق EquipmentClassSeeder) - یک Activity فقط روی PUMP تعریف
        // می‌شود و باید برای تجهیزی با کلاس دقیق PUMP_CENT هم قابل‌مشاهده باشد.
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_Activity_InheritedFromParentClass_WhenNotDefinedOnExactClass));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var pumpClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP");
        var group = await ctx.MaintenanceGroups.FirstAsync();

        var activity = new MaintenanceActivity
        {
            Code = "TEST_PUMP_PARENT_ACT", Name = "Parent-Level Pump Activity", MaintenanceGroupId = group.MaintenanceGroupId,
            IsActive = true, IsSystemDefault = false, CreatedDate = DateTime.Now
        };
        ctx.MaintenanceActivities.Add(activity);
        await ctx.SaveChangesAsync();

        ctx.MaintenanceActivityEquipmentClasses.Add(new MaintenanceActivityEquipmentClass
        {
            MaintenanceActivityId = activity.MaintenanceActivityId,
            EquipmentClassId = pumpClass.EquipmentClassId
        });

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(1);

        Assert.NotNull(result);
        var inherited = result!.ApplicableActivities.SingleOrDefault(x => x.Code == "TEST_PUMP_PARENT_ACT");
        Assert.NotNull(inherited);
        Assert.Contains("Inherited from", inherited!.Source);
        Assert.Contains("Pump", inherited.Source);
    }

    [Fact]
    public async Task IntelligenceService_Activity_ExactClassOverridesParent_NoDuplicate()
    {
        // همان Activity هم روی PUMP_CENT (دقیق) هم روی PUMP (والد) تعریف می‌شود - نتیجه باید
        // دقیقاً یک بار برگردد و منبع آن باید "Exact match" باشد، نه "Inherited".
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_Activity_ExactClassOverridesParent_NoDuplicate));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var pumpClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP");
        var group = await ctx.MaintenanceGroups.FirstAsync();

        var activity = new MaintenanceActivity
        {
            Code = "TEST_DUAL_LEVEL_ACT", Name = "Dual Level Activity", MaintenanceGroupId = group.MaintenanceGroupId,
            IsActive = true, IsSystemDefault = false, CreatedDate = DateTime.Now
        };
        ctx.MaintenanceActivities.Add(activity);
        await ctx.SaveChangesAsync();

        ctx.MaintenanceActivityEquipmentClasses.Add(new MaintenanceActivityEquipmentClass { MaintenanceActivityId = activity.MaintenanceActivityId, EquipmentClassId = pumpClass.EquipmentClassId });
        ctx.MaintenanceActivityEquipmentClasses.Add(new MaintenanceActivityEquipmentClass { MaintenanceActivityId = activity.MaintenanceActivityId, EquipmentClassId = pumpCentClass.EquipmentClassId });

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(1);

        Assert.NotNull(result);
        var matches = result!.ApplicableActivities.Where(x => x.Code == "TEST_DUAL_LEVEL_ACT").ToList();
        Assert.Single(matches); // بدون تکرار
        Assert.Contains("Exact match", matches[0].Source);
    }

    [Fact]
    public async Task IntelligenceService_GrandparentInheritance_Works()
    {
        // PUMP_CENT → PUMP → ROTATING_EQUIPMENT: یک Activity فقط روی جد (ROTATING_EQUIPMENT)
        // تعریف می‌شود و باید تا دو سطح پایین‌تر هم به ارث برسد.
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_GrandparentInheritance_Works));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var rotatingClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "ROTATING_EQUIPMENT");
        var group = await ctx.MaintenanceGroups.FirstAsync();

        var activity = new MaintenanceActivity
        {
            Code = "TEST_GRANDPARENT_ACT", Name = "Grandparent-Level Activity", MaintenanceGroupId = group.MaintenanceGroupId,
            IsActive = true, IsSystemDefault = false, CreatedDate = DateTime.Now
        };
        ctx.MaintenanceActivities.Add(activity);
        await ctx.SaveChangesAsync();

        ctx.MaintenanceActivityEquipmentClasses.Add(new MaintenanceActivityEquipmentClass
        {
            MaintenanceActivityId = activity.MaintenanceActivityId,
            EquipmentClassId = rotatingClass.EquipmentClassId
        });

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(1);

        Assert.NotNull(result);
        Assert.Contains(result!.ApplicableActivities, x => x.Code == "TEST_GRANDPARENT_ACT");
        Assert.Equal(3, result.ResolvedClassHierarchy.Count); // PUMP_CENT, PUMP, ROTATING_EQUIPMENT
    }

    [Fact]
    public async Task IntelligenceService_PMTemplate_InheritedFromParentClass()
    {
        var ctx = await SeedFullBaselineAsync(nameof(IntelligenceService_PMTemplate_InheritedFromParentClass));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");

        // PMT_PUMP_CENT_MONTHLY از قبل روی خودِ PUMP_CENT (نه PUMP) تعریف شده - نتیجه باید
        // "Exact match" باشد نه "Inherited"
        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new MaintenanceIntelligenceService(ctx);
        var result = await service.GetForEquipmentAsync(1);

        Assert.NotNull(result);
        var template = result!.CandidatePMTemplates.Single(x => x.Code == "PMT_PUMP_CENT_MONTHLY");
        Assert.Contains("Exact match", template.Source);
    }

    // ================= Phase 3.5: پروفایل مجدد داده + قاعده‌ی پیشوندی + توضیح قاعده =================

    [Fact]
    public async Task Backfill_HighConfidencePrefixRule_ClassifiesLocationSuffixedNames()
    {
        // طبق کشف فاز ۳.۵: بسیاری از رکوردهای واقعی F&G DEVICE یک پسوند محل نصب دارند
        // (با یک خط‌جدید واقعی) - مثلاً "TOXIC GAS DETECTOR\n( HVAC ROOM )". قاعده‌ی پیشوندی
        // باید این‌ها را هم به همان اندازه‌ی تطابق دقیق، قطعی طبقه‌بندی کند.
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_HighConfidencePrefixRule_ClassifiesLocationSuffixedNames));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "82-XA-100", Name = "TOXIC GAS DETECTOR\n( HVAC ROOM )", EquipmentType = "F&G DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(1, result.AutoClassifiedCount);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Equal("HIGH", equipment.ClassificationConfidence);

        var equipmentClass = await ctx.EquipmentClasses.SingleAsync(x => x.EquipmentClassId == equipment.EquipmentClassId);
        Assert.Equal("GAS_DETECTOR", equipmentClass.Code);
    }

    [Fact]
    public async Task Backfill_HighConfidencePrefixRule_DoesNotMatchUnrelatedPrefix()
    {
        // یک نام که با هیچ پیشوند شناخته‌شده‌ای شروع نمی‌شود نباید به‌اشتباه تطبیق پیدا کند.
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_HighConfidencePrefixRule_DoesNotMatchUnrelatedPrefix));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "82-XA-101", Name = "UNKNOWN F&G DEVICE VARIANT", EquipmentType = "F&G DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var result = await service.BackfillAsync();

        Assert.Equal(0, result.AutoClassifiedCount);
        Assert.Equal(0, result.SuggestionsCreatedCount);
    }

    [Fact]
    public async Task Backfill_AddingNewRules_NeverTouchesAlreadyClassifiedEquipment()
    {
        // طبق بند ۳۱ (Phase 3.5 Data Safety): گسترش دیکشنری قواعد هرگز نباید رکوردهایی که از
        // فازهای قبل طبقه‌بندی شده‌اند را دوباره بنویسد - Backfill فقط EquipmentClassId=Null را می‌بیند.
        var ctx = await SeedFullBaselineAsync(nameof(Backfill_AddingNewRules_NeverTouchesAlreadyClassifiedEquipment));

        var motorClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "MOTOR");
        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "compressor",
            EquipmentClassId = motorClass.EquipmentClassId, ClassificationConfidence = "MANUAL", ClassificationSource = "MANUAL",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        await service.BackfillAsync();

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.Equal(motorClass.EquipmentClassId, equipment.EquipmentClassId);
        Assert.Equal("MANUAL", equipment.ClassificationSource);
    }

    [Fact]
    public async Task ExplainClassification_ExactTier_ReturnsRuleAndMatchedInput()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ExplainClassification_ExactTier_ReturnsRuleAndMatchedInput));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "compressor",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var explanation = await service.ExplainClassificationAsync(1);

        Assert.NotNull(explanation);
        Assert.Equal("EXACT", explanation!.MatchedRuleTier);
        Assert.Contains("EquipmentType", explanation.MatchedInput);
        Assert.Equal("COMPRESSOR", explanation.ResolvedEquipmentClassCode);
    }

    [Fact]
    public async Task ExplainClassification_PrefixTier_ReturnsPrefixMatchedInput()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ExplainClassification_PrefixTier_ReturnsPrefixMatchedInput));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "MANUAL CALL POINT (OUTDOOR)", EquipmentType = "F&G DEVICE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var explanation = await service.ExplainClassificationAsync(1);

        Assert.NotNull(explanation);
        Assert.Contains("prefix", explanation!.MatchedRuleTier);
        Assert.Equal("MANUAL_CALL_POINT", explanation.ResolvedEquipmentClassCode);
    }

    [Fact]
    public async Task ExplainClassification_NoRuleMatches_ReturnsNullTier()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ExplainClassification_NoRuleMatches_ReturnsNullTier));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "CABLE", EquipmentType = "CABLE",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var explanation = await service.ExplainClassificationAsync(1);

        Assert.NotNull(explanation);
        Assert.Null(explanation!.MatchedRuleTier);
    }

    // ================= Phase 3.5: Maintenance Intelligence Coverage Matrix =================

    [Fact]
    public async Task CoverageMatrix_DirectVsInherited_AreCorrectlySeparated()
    {
        var ctx = await SeedFullBaselineAsync(nameof(CoverageMatrix_DirectVsInherited_AreCorrectlySeparated));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        var pumpClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP");
        var group = await ctx.MaintenanceGroups.FirstAsync();

        var parentActivity = new MaintenanceActivity
        {
            Code = "TEST_COVERAGE_PARENT_ACT", Name = "Parent Activity", MaintenanceGroupId = group.MaintenanceGroupId,
            IsActive = true, IsSystemDefault = false, CreatedDate = DateTime.Now
        };
        ctx.MaintenanceActivities.Add(parentActivity);
        await ctx.SaveChangesAsync();

        ctx.MaintenanceActivityEquipmentClasses.Add(new MaintenanceActivityEquipmentClass
        {
            MaintenanceActivityId = parentActivity.MaintenanceActivityId,
            EquipmentClassId = pumpClass.EquipmentClassId
        });

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassCoverageService(ctx);
        var matrix = await service.GetCoverageMatrixAsync();

        var pumpCentRow = matrix.Classes.Single(x => x.Code == "PUMP_CENT");
        Assert.Equal(1, pumpCentRow.EquipmentCount);
        Assert.True(pumpCentRow.InheritedActivityCount >= 1); // از PUMP به ارث رسیده

        var pumpRow = matrix.Classes.Single(x => x.Code == "PUMP");
        Assert.True(pumpRow.DirectActivityCount >= 1); // مستقیماً روی خودِ PUMP تعریف شده
    }

    [Fact]
    public async Task CoverageMatrix_ClassWithEquipmentButNoActivities_FlagsGap()
    {
        var ctx = await SeedFullBaselineAsync(nameof(CoverageMatrix_ClassWithEquipmentButNoActivities_FlagsGap));

        // CHILLER (زیر HVAC_EQUIPMENT، فاز ۲) هیچ Activity/PMTemplate/FailureMode ای ندارد - نه
        // مستقیم نه به‌ارث‌رسیده، چون MaintenanceActivitySeeder فاز ۱ فقط تجهیزات دوار/ثابت/برقی
        // را هدف گرفته بود، نه HVAC.
        var chillerClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "CHILLER");

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "chiller",
            EquipmentClassId = chillerClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassCoverageService(ctx);
        var matrix = await service.GetCoverageMatrixAsync();

        var chillerRow = matrix.Classes.Single(x => x.Code == "CHILLER");
        Assert.Equal(1, chillerRow.EquipmentCount);
        Assert.Equal(0, chillerRow.TotalActivityCoverage);
        Assert.Contains(chillerRow.GapReasons, g => g.Contains("Missing applicability"));
        Assert.Contains(chillerRow.GapReasons, g => g.Contains("Missing PM template"));
        Assert.Contains(chillerRow.GapReasons, g => g.Contains("Missing failure taxonomy"));
    }

    [Fact]
    public async Task CoverageMatrix_ClassWithNoEquipmentAndNoMasterData_IsMarkedIntentional()
    {
        var ctx = await SeedFullBaselineAsync(nameof(CoverageMatrix_ClassWithNoEquipmentAndNoMasterData_IsMarkedIntentional));

        var service = new EquipmentClassCoverageService(ctx);
        var matrix = await service.GetCoverageMatrixAsync();

        // یک کلاس خیلی خاص که هیچ‌وقت Equipment/Activity ندارد (مثلاً یکی از کلاس‌های تازه‌ی فاز ۳)
        var emptyRow = matrix.Classes.Single(x => x.Code == "BATTERY");
        Assert.Equal(0, emptyRow.EquipmentCount);
        Assert.Contains(emptyRow.GapReasons, g => g.StartsWith("Intentional"));
    }

    [Fact]
    public async Task CoverageMatrix_SummaryPercentages_OnlyCountClassesWithEquipment()
    {
        var ctx = await SeedFullBaselineAsync(nameof(CoverageMatrix_SummaryPercentages_OnlyCountClassesWithEquipment));

        var pumpCentClass = await ctx.EquipmentClasses.SingleAsync(x => x.Code == "PUMP_CENT");
        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "pump",
            EquipmentClassId = pumpCentClass.EquipmentClassId,
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassCoverageService(ctx);
        var matrix = await service.GetCoverageMatrixAsync();

        Assert.Equal(1, matrix.ClassesWithEquipment);
        // PUMP_CENT از قبل PMT_PUMP_CENT_MONTHLY و MaintenanceActivity های فاز ۱ را دارد
        Assert.True(matrix.PMTemplateCoveragePercent > 0);
    }
}
