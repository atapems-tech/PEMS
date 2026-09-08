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
        var applied = await service.BackfillAsync();

        Assert.Equal(1, applied);

        var equipment = await ctx.Equipments.AsNoTracking().SingleAsync(x => x.EquipmentId == 1);
        Assert.NotNull(equipment.EquipmentClassId);
        Assert.Equal("EXACT", equipment.ClassificationConfidence);
        Assert.Equal("RULE", equipment.ClassificationSource);

        var equipmentClass = await ctx.EquipmentClasses.SingleAsync(x => x.EquipmentClassId == equipment.EquipmentClassId);
        Assert.Equal("COMPRESSOR", equipmentClass.Code);
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
        var applied = await service.BackfillAsync();

        Assert.Equal(0, applied);

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
        var applied = await service.BackfillAsync();

        Assert.Equal(0, applied); // چون EquipmentClassId از قبل مقداردهی شده - Backfill کاری نمی‌کند

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

        Assert.Equal(1, firstRun);
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task ClassificationReport_ListsUnmatchedEquipment_ForHumanReview()
    {
        var ctx = await SeedFullBaselineAsync(nameof(ClassificationReport_ListsUnmatchedEquipment_ForHumanReview));

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1, TagNumber = "EQ-1", Name = "EQ-1", EquipmentType = "PANEL",
            ProjectId = 1, DisciplineId = 1, IsActive = true, CreatedDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var service = new EquipmentClassificationService(ctx);
        var report = await service.GetClassificationReportAsync();

        Assert.Equal(1, report.TotalUnclassifiedEquipment);
        Assert.Equal(1, report.UnmatchedNeedsReview);
        Assert.Equal(0, report.MatchedByRule);
        Assert.Contains(report.Suggestions, s => s.EquipmentId == 1 && s.ProposedEquipmentClassCode == null);
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
}
