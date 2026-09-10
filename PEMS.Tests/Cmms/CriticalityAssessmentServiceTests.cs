using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 7.6 — Engineering Criticality Assessment & Controlled Maintenance
/// Activation»: تست‌های گردش‌کار DRAFT→UNDER_REVIEW→APPROVED→SUPERSEDED، محاسبه‌ی قطعی
/// (Max-based)، الزام شواهد، تفکیک وظایف (سازنده ≠ تایید‌کننده)، و انتشار در
/// EquipmentCriticality موجود فقط در لحظه‌ی Approve.
/// </summary>
public class CriticalityAssessmentServiceTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, int EquipmentId)> SeedEquipmentAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase76_{dbName}");
        var equipment = new Equipment { TagNumber = $"EQ-{dbName}", Name = $"EQ-{dbName}", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        return (ctx, equipment.EquipmentId);
    }

    // ================= Framework: Deterministic Calculation =================

    [Fact]
    public void Framework_ClassFromMaxScore_MapsExactlyPerDocumentedThresholds()
    {
        Assert.Null(CriticalityFramework.ClassFromMaxScore(null));
        Assert.Equal("C", CriticalityFramework.ClassFromMaxScore(1));
        Assert.Equal("C", CriticalityFramework.ClassFromMaxScore(2));
        Assert.Equal("B", CriticalityFramework.ClassFromMaxScore(3));
        Assert.Equal("A", CriticalityFramework.ClassFromMaxScore(4));
        Assert.Equal("A", CriticalityFramework.ClassFromMaxScore(5));
    }

    [Fact]
    public void Assessment_CalculatedMaxScore_GovernedByWorstDimension_NotAverage()
    {
        // طبق بند ۸: «بدترین بُعد حاکم است» - Safety=5 نباید توسط Production/Environment پایین رقیق شود
        var assessment = new CriticalityAssessment { SafetyScore = 5, ProductionScore = 1, EnvironmentScore = 1 };

        Assert.Equal(5, assessment.CalculatedMaxScore);
        Assert.Equal("A", assessment.ProposedCriticalityClass);
    }

    [Fact]
    public void Assessment_IncompleteScores_CalculatedMaxScoreIsNull_NeverFabricated()
    {
        var assessment = new CriticalityAssessment { SafetyScore = 5, ProductionScore = null, EnvironmentScore = 2 };

        Assert.Null(assessment.CalculatedMaxScore);
        Assert.Null(assessment.ProposedCriticalityClass);
    }

    // ================= Workflow: Draft → Under Review =================

    [Fact]
    public async Task CreateDraftAsync_ValidEquipment_CreatesDraftAssessment()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(CreateDraftAsync_ValidEquipment_CreatesDraftAssessment));
        var service = new CriticalityAssessmentService(ctx);

        var (success, error, id) = await service.CreateDraftAsync(equipmentId, "engineer1", "initial assessment");

        Assert.True(success);
        Assert.Null(error);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Equal("DRAFT", assessment.Status);
        Assert.Equal(CriticalityFramework.CurrentVersion, assessment.FrameworkVersion);
    }

    [Fact]
    public async Task CreateDraftAsync_InvalidEquipment_ReturnsError_NoRowCreated()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase76_{nameof(CreateDraftAsync_InvalidEquipment_ReturnsError_NoRowCreated)}");
        var service = new CriticalityAssessmentService(ctx);

        var (success, error, id) = await service.CreateDraftAsync(99999, "engineer1", null);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Empty(ctx.CriticalityAssessments);
    }

    [Fact]
    public async Task CreateDraftAsync_ExistingOpenAssessment_Rejected_NoDuplicate()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(CreateDraftAsync_ExistingOpenAssessment_Rejected_NoDuplicate));
        var service = new CriticalityAssessmentService(ctx);

        var first = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        var second = await service.CreateDraftAsync(equipmentId, "engineer2", null);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Single(ctx.CriticalityAssessments);
    }

    [Fact]
    public async Task SetDimensionScoresAsync_OutOfRangeScore_Rejected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(SetDimensionScoresAsync_OutOfRangeScore_Rejected));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);

        var result = await service.SetDimensionScoresAsync(id!.Value, safetyScore: 6, productionScore: 3, environmentScore: 2, null);

        Assert.False(result.Success);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Null(assessment.SafetyScore); // هیچ امتیازی ذخیره نشد
    }

    [Fact]
    public async Task SetDimensionScoresAsync_OnApprovedAssessment_Rejected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(SetDimensionScoresAsync_OnApprovedAssessment_Rejected));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 3, 3, 3, null);
        await service.AddEvidenceAsync(id.Value, "Equipment", null, null, "test evidence", "engineer1");
        await service.SubmitForReviewAsync(id.Value, "engineer1");
        await service.ApproveAsync(id.Value, "supervisor1");

        var result = await service.SetDimensionScoresAsync(id.Value, 5, 5, 5, "trying to change after approval");

        Assert.False(result.Success);
    }

    // ================= Workflow: Evidence Requirement =================

    [Fact]
    public async Task SubmitForReviewAsync_NoEvidence_Rejected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(SubmitForReviewAsync_NoEvidence_Rejected));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 3, 3, 3, null);

        var result = await service.SubmitForReviewAsync(id.Value, "engineer1");

        Assert.False(result.Success);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Equal("DRAFT", assessment.Status);
    }

    [Fact]
    public async Task SubmitForReviewAsync_IncompleteScores_Rejected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(SubmitForReviewAsync_IncompleteScores_Rejected));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 3, null, 3, null); // Production missing
        await service.AddEvidenceAsync(id.Value, "Equipment", null, null, "test evidence", "engineer1");

        var result = await service.SubmitForReviewAsync(id.Value, "engineer1");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SubmitForReviewAsync_ScoresAndEvidencePresent_Succeeds()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(SubmitForReviewAsync_ScoresAndEvidencePresent_Succeeds));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 3, 3, 3, null);
        await service.AddEvidenceAsync(id.Value, "Equipment", "EQ-1", null, "P&ID reference", "engineer1");

        var result = await service.SubmitForReviewAsync(id.Value, "engineer1");

        Assert.True(result.Success);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Equal("UNDER_REVIEW", assessment.Status);
    }

    [Fact]
    public async Task AddEvidenceAsync_EmptyDescription_Rejected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(AddEvidenceAsync_EmptyDescription_Rejected));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);

        var result = await service.AddEvidenceAsync(id!.Value, "Equipment", null, null, "  ", "engineer1");

        Assert.False(result.Success);
        Assert.Empty(ctx.CriticalityAssessmentEvidences);
    }

    // ================= Workflow: Approval + SoD + Publication =================

    [Fact]
    public async Task ApproveAsync_SamePreparerAndApprover_Rejected_SoD()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(ApproveAsync_SamePreparerAndApprover_Rejected_SoD));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 3, 3, 3, null);
        await service.AddEvidenceAsync(id.Value, "Equipment", null, null, "evidence", "engineer1");
        await service.SubmitForReviewAsync(id.Value, "engineer1");

        var result = await service.ApproveAsync(id.Value, "engineer1"); // همان سازنده

        Assert.False(result.Success);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Equal("UNDER_REVIEW", assessment.Status); // تغییر نکرد
        Assert.Empty(ctx.EquipmentCriticalities); // چیزی منتشر نشد
    }

    [Fact]
    public async Task ApproveAsync_DifferentApprover_Succeeds_PublishesToEquipmentCriticality()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(ApproveAsync_DifferentApprover_Succeeds_PublishesToEquipmentCriticality));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, safetyScore: 5, productionScore: 2, environmentScore: 3, null);
        await service.AddEvidenceAsync(id.Value, "Equipment", null, null, "evidence", "engineer1");
        await service.SubmitForReviewAsync(id.Value, "engineer1");

        var result = await service.ApproveAsync(id.Value, "supervisor1");

        Assert.True(result.Success);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Equal("APPROVED", assessment.Status);
        Assert.Equal("supervisor1", assessment.ApprovedBy);

        var published = await ctx.EquipmentCriticalities.AsNoTracking().SingleAsync();
        Assert.Equal("A", published.CriticalityClass); // max(5,2,3) = 5 -> A
        Assert.True(published.IsActive);
    }

    [Fact]
    public async Task ApproveAsync_NonUnderReviewStatus_Rejected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(ApproveAsync_NonUnderReviewStatus_Rejected));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null); // still DRAFT

        var result = await service.ApproveAsync(id!.Value, "supervisor1");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApproveAsync_ExistingActiveCriticality_DeactivatedNotDeleted()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(ApproveAsync_ExistingActiveCriticality_DeactivatedNotDeleted));
        ctx.EquipmentCriticalities.Add(new EquipmentCriticality { EquipmentId = equipmentId, CriticalityClass = "C", IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 5, 5, 5, null);
        await service.AddEvidenceAsync(id.Value, "Equipment", null, null, "evidence", "engineer1");
        await service.SubmitForReviewAsync(id.Value, "engineer1");
        await service.ApproveAsync(id.Value, "supervisor1");

        var all = await ctx.EquipmentCriticalities.AsNoTracking().ToListAsync();
        Assert.Equal(2, all.Count); // قدیمی حذف نشد، فقط غیرفعال شد
        Assert.Single(all, x => x.IsActive); // فقط یکی فعال است
        Assert.Equal("A", all.Single(x => x.IsActive).CriticalityClass);
    }

    [Fact]
    public async Task ApproveAsync_PriorApprovedAssessment_SupersededNotDeleted()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(ApproveAsync_PriorApprovedAssessment_SupersededNotDeleted));
        var service = new CriticalityAssessmentService(ctx);

        // اولین چرخه‌ی کامل
        var (_, _, id1) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id1!.Value, 2, 2, 2, null);
        await service.AddEvidenceAsync(id1.Value, "Equipment", null, null, "evidence 1", "engineer1");
        await service.SubmitForReviewAsync(id1.Value, "engineer1");
        await service.ApproveAsync(id1.Value, "supervisor1");

        // چرخه‌ی دوم (بازارزیابی)
        var (_, _, id2) = await service.CreateDraftAsync(equipmentId, "engineer2", null);
        await service.SetDimensionScoresAsync(id2!.Value, 5, 5, 5, null);
        await service.AddEvidenceAsync(id2.Value, "Equipment", null, null, "evidence 2", "engineer2");
        await service.SubmitForReviewAsync(id2.Value, "engineer2");
        await service.ApproveAsync(id2.Value, "supervisor2");

        var first = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync(x => x.CriticalityAssessmentId == id1.Value);
        Assert.Equal("SUPERSEDED", first.Status);
        Assert.Equal(id2.Value, first.SupersededByAssessmentId);

        var second = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync(x => x.CriticalityAssessmentId == id2.Value);
        Assert.Equal("APPROVED", second.Status);

        Assert.Equal(2, await ctx.CriticalityAssessments.CountAsync()); // هیچ‌کدام حذف نشد
        var publishedActive = await ctx.EquipmentCriticalities.Where(x => x.IsActive).ToListAsync();
        Assert.Single(publishedActive);
        Assert.Equal("A", publishedActive[0].CriticalityClass); // آخرین (دومی) منتشرشده
    }

    // ================= Workflow: Reject =================

    [Fact]
    public async Task RejectAsync_UnderReview_ReturnsToDraft()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(RejectAsync_UnderReview_ReturnsToDraft));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);
        await service.SetDimensionScoresAsync(id!.Value, 3, 3, 3, null);
        await service.AddEvidenceAsync(id.Value, "Equipment", null, null, "evidence", "engineer1");
        await service.SubmitForReviewAsync(id.Value, "engineer1");

        var result = await service.RejectAsync(id.Value, "supervisor1");

        Assert.True(result.Success);
        var assessment = await ctx.CriticalityAssessments.AsNoTracking().SingleAsync();
        Assert.Equal("DRAFT", assessment.Status);
        Assert.Empty(ctx.EquipmentCriticalities); // چیزی منتشر نشد
    }

    // ================= Dashboard / Queue =================

    [Fact]
    public async Task GetDashboardAsync_NoFabricatedZero_ReflectsRealCounts()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(GetDashboardAsync_NoFabricatedZero_ReflectsRealCounts));
        var service = new CriticalityAssessmentService(ctx);
        var (_, _, id) = await service.CreateDraftAsync(equipmentId, "engineer1", null);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(1, dashboard.TotalActiveEquipment);
        Assert.Equal(1, dashboard.Draft);
        Assert.Equal(1, dashboard.Assessed);
        Assert.Equal(0, dashboard.Unassessed);
        Assert.Equal(0, dashboard.Approved);
    }

    [Fact]
    public async Task GetQueueAsync_FiltersByStatus()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(GetQueueAsync_FiltersByStatus));
        var service = new CriticalityAssessmentService(ctx);
        await service.CreateDraftAsync(equipmentId, "engineer1", null);

        var draftQueue = await service.GetQueueAsync(status: "DRAFT");
        var approvedQueue = await service.GetQueueAsync(status: "APPROVED");

        Assert.Single(draftQueue.Items);
        Assert.Empty(approvedQueue.Items);
    }
}
