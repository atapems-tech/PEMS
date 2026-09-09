using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 7 — Maintenance Intelligence & Reliability Operationalization»: تست‌های
/// PM Compliance، شکست Corrective/Preventive/Emergency، خرابی تکراری، اثربخشی نگهداری،
/// ماتریس بحرانی×خرابی، و حلقه‌ی بازخورد RCM (شواهد + مرز تایید انسانی).
/// </summary>
public class Phase7IntelligenceTests
{
    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, int EquipmentId)> SeedEquipmentAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase7_{dbName}");
        var equipment = new Equipment { TagNumber = $"EQ-{dbName}", Name = $"EQ-{dbName}", EquipmentType = "Pump", IsActive = true };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        return (ctx, equipment.EquipmentId);
    }

    private static WorkOrder MakePmWorkOrder(int equipmentId, int planId, string status, DateTime? dueDate, DateTime? completionDate, string number) =>
        new()
        {
            EquipmentId = equipmentId,
            MaintenancePlanId = planId,
            WorkOrderNumber = number,
            WorkOrderType = "Preventive",
            Status = status,
            DueDate = dueDate,
            CompletionDate = completionDate,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

    // ================= PM Compliance =================

    [Fact]
    public async Task PmCompliance_NoQualifyingWorkOrders_ReturnsNotReady()
    {
        var (ctx, _) = await SeedEquipmentAsync(nameof(PmCompliance_NoQualifyingWorkOrders_ReturnsNotReady));
        var service = new ReliabilityIntelligenceService(ctx);

        var dto = await service.GetPmComplianceAsync(null, null);

        Assert.Equal("NOT_READY", dto.DataStatus);
        Assert.Null(dto.CompliancePercent);
        Assert.Equal(0, dto.TotalQualifyingWorkOrders);
    }

    [Fact]
    public async Task PmCompliance_ClassifiesAllSixBuckets_Correctly()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(PmCompliance_ClassifiesAllSixBuckets_Correctly));
        var now = DateTime.Now;

        ctx.WorkOrders.AddRange(
            MakePmWorkOrder(equipmentId, 1, "Completed", now.AddDays(-10), now.AddDays(-11), "WO-COMPLIANT"), // completed before due -> Compliant
            MakePmWorkOrder(equipmentId, 2, "Closed", now.AddDays(-10), now.AddDays(-5), "WO-LATE"), // completed after due -> CompletedLate
            MakePmWorkOrder(equipmentId, 3, "Open", now.AddDays(-5), null, "WO-OVERDUE"), // still open, due date passed -> Overdue
            MakePmWorkOrder(equipmentId, 4, "Assigned", now.AddDays(5), null, "WO-NOTYETDUE"), // still open, due date in future -> NotYetDue
            MakePmWorkOrder(equipmentId, 5, "Cancelled", now.AddDays(-3), null, "WO-CANCELLED"), // -> Cancelled
            MakePmWorkOrder(equipmentId, 6, "Open", null, null, "WO-NODUEDATE") // no DueDate at all -> InsufficientData
        );
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetPmComplianceAsync(null, null);

        Assert.Equal(6, dto.TotalQualifyingWorkOrders);
        Assert.Equal(1, dto.CompliantCount);
        Assert.Equal(1, dto.CompletedLateCount);
        Assert.Equal(1, dto.OverdueCount);
        Assert.Equal(1, dto.NotYetDueCount);
        Assert.Equal(1, dto.CancelledCount);
        Assert.Equal(1, dto.InsufficientDataCount);

        // مخرج فقط Compliant+CompletedLate+Overdue = 3؛ Cancelled/NotYetDue/InsufficientData هرگز شمرده نمی‌شوند
        Assert.NotNull(dto.CompliancePercent);
        Assert.Equal(33.3, dto.CompliancePercent!.Value, 1);
    }

    [Fact]
    public async Task PmCompliance_SmallSample_ReturnsPartial_NotReady()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(PmCompliance_SmallSample_ReturnsPartial_NotReady));
        var now = DateTime.Now;
        ctx.WorkOrders.Add(MakePmWorkOrder(equipmentId, 1, "Completed", now.AddDays(-10), now.AddDays(-11), "WO-1"));
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetPmComplianceAsync(null, null);

        Assert.Equal("PARTIAL", dto.DataStatus);
        Assert.NotNull(dto.DataStatusReason);
    }

    [Fact]
    public async Task PmCompliance_CorrectiveWorkOrders_NeverCountedAsQualifying()
    {
        // طبق بند ۱۲: فقط WorkOrder هایی که واقعاً از یک MaintenancePlan تولید شده‌اند شمرده می‌شوند
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(PmCompliance_CorrectiveWorkOrders_NeverCountedAsQualifying));
        ctx.WorkOrders.Add(new WorkOrder
        {
            EquipmentId = equipmentId, MaintenancePlanId = null, WorkOrderNumber = "WO-CM-1",
            WorkOrderType = "Corrective", Status = "Completed", CompletionDate = DateTime.Now, IsActive = true
        });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetPmComplianceAsync(null, null);

        Assert.Equal(0, dto.TotalQualifyingWorkOrders);
        Assert.Equal("NOT_READY", dto.DataStatus);
    }

    // ================= Maintenance Type Breakdown =================

    [Fact]
    public async Task MaintenanceTypeBreakdown_NoWorkOrders_ReturnsNotReady()
    {
        var (ctx, _) = await SeedEquipmentAsync(nameof(MaintenanceTypeBreakdown_NoWorkOrders_ReturnsNotReady));
        var service = new ReliabilityIntelligenceService(ctx);

        var dto = await service.GetMaintenanceTypeBreakdownAsync(null, null);

        Assert.Equal("NOT_READY", dto.DataStatus);
        Assert.Null(dto.EmergencyWorkRatioPercent);
    }

    [Fact]
    public async Task MaintenanceTypeBreakdown_UnknownType_BucketedAsOther()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(MaintenanceTypeBreakdown_UnknownType_BucketedAsOther));
        ctx.WorkOrders.Add(new WorkOrder { EquipmentId = equipmentId, WorkOrderNumber = "WO-1", WorkOrderType = "Overhaul", Status = "Open", IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetMaintenanceTypeBreakdownAsync(null, null);

        var other = Assert.Single(dto.Items);
        Assert.Equal("Other", other.TypeName);
        Assert.Equal(1, other.Count);
    }

    [Fact]
    public async Task MaintenanceTypeBreakdown_EmergencyRatio_ComputedFromRecognizedTypeOnly()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(MaintenanceTypeBreakdown_EmergencyRatio_ComputedFromRecognizedTypeOnly));
        ctx.WorkOrders.AddRange(
            new WorkOrder { EquipmentId = equipmentId, WorkOrderNumber = "WO-1", WorkOrderType = "Emergency", Status = "Open", IsActive = true, CreatedDate = DateTime.Now },
            new WorkOrder { EquipmentId = equipmentId, WorkOrderNumber = "WO-2", WorkOrderType = "Preventive", Status = "Open", IsActive = true, CreatedDate = DateTime.Now },
            new WorkOrder { EquipmentId = equipmentId, WorkOrderNumber = "WO-3", WorkOrderType = "Preventive", Status = "Open", IsActive = true, CreatedDate = DateTime.Now },
            new WorkOrder { EquipmentId = equipmentId, WorkOrderNumber = "WO-4", WorkOrderType = "Preventive", Status = "Open", IsActive = true, CreatedDate = DateTime.Now }
        );
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetMaintenanceTypeBreakdownAsync(null, null);

        Assert.Equal(1, dto.EmergencyWorkOrderCount);
        Assert.NotNull(dto.EmergencyWorkRatioPercent);
        Assert.Equal(25.0, dto.EmergencyWorkRatioPercent!.Value, 1);
    }

    [Fact]
    public async Task MaintenanceTypeBreakdown_NoDowntimeRecorded_ReturnsNullNotZero()
    {
        // طبق بند ۱۱: «No Recorded Downtime» ≠ «۰ ساعت» - وقتی هیچ رکوردی DowntimeHours ندارد، Null بماند
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(MaintenanceTypeBreakdown_NoDowntimeRecorded_ReturnsNullNotZero));
        ctx.WorkOrders.Add(new WorkOrder { EquipmentId = equipmentId, WorkOrderNumber = "WO-1", WorkOrderType = "Corrective", Status = "Open", DowntimeHours = null, IsActive = true, CreatedDate = DateTime.Now });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetMaintenanceTypeBreakdownAsync(null, null);

        var corrective = dto.Items.Single(x => x.TypeName == "Corrective");
        Assert.Null(corrective.TotalDowntimeHours);
    }

    // ================= Repeat Failure =================

    [Fact]
    public async Task RepeatFailures_TwoFailuresWithinWindow_DetectedAsRepeat()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(RepeatFailures_TwoFailuresWithinWindow_DetectedAsRepeat));
        var now = DateTime.Now;
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-60), FailureMode = "Leakage", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-10), FailureMode = "Leakage", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var groups = await service.GetRepeatFailuresAsync(null, null, windowDays: 90);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.OccurrenceCount);
        Assert.Equal("Leakage", group.FailureModeKey);
    }

    [Fact]
    public async Task RepeatFailures_TwoFailuresOutsideWindow_NotDetected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(RepeatFailures_TwoFailuresOutsideWindow_NotDetected));
        var now = DateTime.Now;
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-400), FailureMode = "Leakage", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-10), FailureMode = "Leakage", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var groups = await service.GetRepeatFailuresAsync(null, null, windowDays: 90);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task RepeatFailures_SingleFailure_NotDetected()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(RepeatFailures_SingleFailure_NotDetected));
        ctx.FailureRecords.Add(new FailureRecord { EquipmentId = equipmentId, FailureDate = DateTime.Now, FailureMode = "Leakage", IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var groups = await service.GetRepeatFailuresAsync(null, null, windowDays: 90);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task RepeatFailures_DifferentFailureModes_NotGroupedTogether()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(RepeatFailures_DifferentFailureModes_NotGroupedTogether));
        var now = DateTime.Now;
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-20), FailureMode = "Leakage", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-10), FailureMode = "Vibration", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var groups = await service.GetRepeatFailuresAsync(null, null, windowDays: 90);

        Assert.Empty(groups);
    }

    // ================= Maintenance Effectiveness (trend) =================

    [Fact]
    public async Task MaintenanceEffectiveness_NoPriorPeriodData_ReturnsInsufficientData()
    {
        var (ctx, _) = await SeedEquipmentAsync(nameof(MaintenanceEffectiveness_NoPriorPeriodData_ReturnsInsufficientData));
        var service = new ReliabilityIntelligenceService(ctx);

        var dto = await service.GetMaintenanceEffectivenessAsync(90);

        Assert.Equal("INSUFFICIENT_DATA", dto.FailureFrequencyTrend);
        Assert.Equal("INSUFFICIENT_DATA", dto.MttrTrend);
        Assert.Equal("INSUFFICIENT_DATA", dto.DowntimeTrend);
    }

    [Fact]
    public async Task MaintenanceEffectiveness_FailureCountDoubled_ReturnsWorsening()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(MaintenanceEffectiveness_FailureCountDoubled_ReturnsWorsening));
        var now = DateTime.Now;
        // دوره‌ی قبلی (۹۰ تا ۱۸۰ روز پیش): ۲ خرابی
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-150), FailureMode = "A", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-120), FailureMode = "A", IsActive = true },
            // دوره‌ی جاری (۹۰ روز اخیر): ۴ خرابی - دو برابر
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-60), FailureMode = "A", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-40), FailureMode = "A", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-20), FailureMode = "A", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-5), FailureMode = "A", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new ReliabilityIntelligenceService(ctx);
        var dto = await service.GetMaintenanceEffectivenessAsync(90);

        Assert.Equal(2, dto.PriorPeriodFailureCount);
        Assert.Equal(4, dto.CurrentPeriodFailureCount);
        Assert.Equal("WORSENING", dto.FailureFrequencyTrend);
    }

    // ================= Criticality Matrix =================

    [Fact]
    public async Task CriticalityMatrix_NoEquipment_ReturnsEmpty()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase7_{nameof(CriticalityMatrix_NoEquipment_ReturnsEmpty)}");
        var service = new ReliabilityIntelligenceService(ctx);

        var rows = await service.GetCriticalityFailureMatrixAsync();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task CriticalityMatrix_UnclassifiedEquipment_GroupedSeparately()
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(nameof(CriticalityMatrix_UnclassifiedEquipment_GroupedSeparately));
        // عمداً هیچ EquipmentCriticality ای ثبت نمی‌شود
        var service = new ReliabilityIntelligenceService(ctx);

        var rows = await service.GetCriticalityFailureMatrixAsync();

        var row = Assert.Single(rows);
        Assert.Equal("Unclassified", row.CriticalityClass);
        Assert.Equal(1, row.EquipmentCount);
        Assert.Empty(row.Findings); // Unclassified هرگز واجد شرایط یافته‌های High/Low Criticality نیست
    }

    // ================= RCM Feedback: Evidence Generation =================

    private static async Task<(PEMS.Persistence.Context.PEMSDbContext Ctx, int EquipmentId, int StrategyId)> SeedStrategyAsync(string dbName)
    {
        var (ctx, equipmentId) = await SeedEquipmentAsync(dbName);
        var strategy = new MaintenanceStrategy { EquipmentId = equipmentId, StrategyType = "Preventive", FailureModeCode = "LEAK", IsActive = true };
        ctx.MaintenanceStrategies.Add(strategy);
        await ctx.SaveChangesAsync();
        return (ctx, equipmentId, strategy.MaintenanceStrategyId);
    }

    [Fact]
    public async Task RcmFeedback_GenerateEvidence_RepeatFailureTrigger_CreatesPendingRecommendation()
    {
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_GenerateEvidence_RepeatFailureTrigger_CreatesPendingRecommendation));
        var now = DateTime.Now;
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-60), FailureMode = "LEAK", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-10), FailureMode = "LEAK", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new RcmFeedbackService(ctx);
        var result = await service.GenerateEvidenceAsync(windowDays: 90, downtimeThresholdHours: 24);

        Assert.True(result.RecommendationsGenerated > 0);
        var recommendation = await ctx.RcmReviewRecommendations.SingleAsync(x => x.TriggerReason == "RepeatFailure");
        Assert.Equal("PENDING", recommendation.Status);
        Assert.Equal(strategyId, recommendation.MaintenanceStrategyId);
    }

    [Fact]
    public async Task RcmFeedback_GenerateEvidence_NoFailures_NoRecommendationCreated()
    {
        var (ctx, _, _) = await SeedStrategyAsync(nameof(RcmFeedback_GenerateEvidence_NoFailures_NoRecommendationCreated));
        var service = new RcmFeedbackService(ctx);

        var result = await service.GenerateEvidenceAsync();

        Assert.Equal(0, result.RecommendationsGenerated);
        Assert.Empty(ctx.RcmReviewRecommendations);
    }

    [Fact]
    public async Task RcmFeedback_GenerateEvidence_AlreadyPending_DoesNotDuplicate()
    {
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_GenerateEvidence_AlreadyPending_DoesNotDuplicate));
        var now = DateTime.Now;
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-60), FailureMode = "LEAK", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-10), FailureMode = "LEAK", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new RcmFeedbackService(ctx);
        var first = await service.GenerateEvidenceAsync();
        var second = await service.GenerateEvidenceAsync();

        Assert.True(first.RecommendationsGenerated > 0);
        Assert.Equal(0, second.RecommendationsGenerated);
        Assert.True(second.StrategiesSkippedAlreadyPending > 0);

        var repeatFailureRecommendations = await ctx.RcmReviewRecommendations
            .Where(x => x.MaintenanceStrategyId == strategyId && x.TriggerReason == "RepeatFailure")
            .CountAsync();
        Assert.Equal(1, repeatFailureRecommendations); // هرگز تکراری ساخته نشد
    }

    [Fact]
    public async Task RcmFeedback_GenerateEvidence_NeverModifiesMaintenanceStrategy()
    {
        // طبق بند ۲۰: تولید شواهد هرگز MaintenanceStrategy را تغییر نمی‌دهد.
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_GenerateEvidence_NeverModifiesMaintenanceStrategy));
        var now = DateTime.Now;
        ctx.FailureRecords.AddRange(
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-60), FailureMode = "LEAK", IsActive = true },
            new FailureRecord { EquipmentId = equipmentId, FailureDate = now.AddDays(-10), FailureMode = "LEAK", IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var strategyBefore = await ctx.MaintenanceStrategies.AsNoTracking().SingleAsync(x => x.MaintenanceStrategyId == strategyId);

        var service = new RcmFeedbackService(ctx);
        await service.GenerateEvidenceAsync();

        var strategyAfter = await ctx.MaintenanceStrategies.AsNoTracking().SingleAsync(x => x.MaintenanceStrategyId == strategyId);
        Assert.Equal(strategyBefore.StrategyType, strategyAfter.StrategyType);
        Assert.Equal(strategyBefore.FailureModeCode, strategyAfter.FailureModeCode);
        Assert.Equal(strategyBefore.Justification, strategyAfter.Justification);
    }

    // ================= RCM Feedback: Human Review Boundary =================

    [Fact]
    public async Task RcmFeedback_ReviewRecommendation_Accept_SetsStatusAndReviewer()
    {
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_ReviewRecommendation_Accept_SetsStatusAndReviewer));
        var recommendation = new RcmReviewRecommendation
        {
            EquipmentId = equipmentId, MaintenanceStrategyId = strategyId, TriggerReason = "HighDowntime",
            Evidence = "test evidence", WindowDays = 90, Status = "PENDING", GeneratedDate = DateTime.Now, IsActive = true
        };
        ctx.RcmReviewRecommendations.Add(recommendation);
        await ctx.SaveChangesAsync();

        var service = new RcmFeedbackService(ctx);
        var (success, error) = await service.ReviewRecommendationAsync(recommendation.RcmReviewRecommendationId, accepted: true, "supervisor1", "Agreed - will revise strategy.");

        Assert.True(success);
        Assert.Null(error);
        var reloaded = await ctx.RcmReviewRecommendations.AsNoTracking().SingleAsync();
        Assert.Equal("ACCEPTED", reloaded.Status);
        Assert.Equal("supervisor1", reloaded.ReviewedBy);
        Assert.NotNull(reloaded.ReviewedAt);
    }

    [Fact]
    public async Task RcmFeedback_ReviewRecommendation_Reject_SetsStatusAndReviewer()
    {
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_ReviewRecommendation_Reject_SetsStatusAndReviewer));
        var recommendation = new RcmReviewRecommendation
        {
            EquipmentId = equipmentId, MaintenanceStrategyId = strategyId, TriggerReason = "HighDowntime",
            Evidence = "test evidence", WindowDays = 90, Status = "PENDING", GeneratedDate = DateTime.Now, IsActive = true
        };
        ctx.RcmReviewRecommendations.Add(recommendation);
        await ctx.SaveChangesAsync();

        var service = new RcmFeedbackService(ctx);
        var (success, _) = await service.ReviewRecommendationAsync(recommendation.RcmReviewRecommendationId, accepted: false, "supervisor1", "Not convincing yet.");

        Assert.True(success);
        var reloaded = await ctx.RcmReviewRecommendations.AsNoTracking().SingleAsync();
        Assert.Equal("REJECTED", reloaded.Status);
    }

    [Fact]
    public async Task RcmFeedback_ReviewRecommendation_AlreadyReviewed_ReturnsError()
    {
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_ReviewRecommendation_AlreadyReviewed_ReturnsError));
        var recommendation = new RcmReviewRecommendation
        {
            EquipmentId = equipmentId, MaintenanceStrategyId = strategyId, TriggerReason = "HighDowntime",
            Evidence = "test evidence", WindowDays = 90, Status = "PENDING", GeneratedDate = DateTime.Now, IsActive = true
        };
        ctx.RcmReviewRecommendations.Add(recommendation);
        await ctx.SaveChangesAsync();

        var service = new RcmFeedbackService(ctx);
        var first = await service.ReviewRecommendationAsync(recommendation.RcmReviewRecommendationId, true, "supervisor1", null);
        var second = await service.ReviewRecommendationAsync(recommendation.RcmReviewRecommendationId, false, "supervisor2", null);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.NotNull(second.Error);
        var reloaded = await ctx.RcmReviewRecommendations.AsNoTracking().SingleAsync();
        Assert.Equal("ACCEPTED", reloaded.Status); // دومین تلاش وضعیت را عوض نکرد
    }

    [Fact]
    public async Task RcmFeedback_ReviewRecommendation_NeverModifiesMaintenanceStrategyOrPlan()
    {
        // طبق بند ۲۰: پذیرفتن یک توصیه هرگز خودش MaintenanceStrategy/MaintenancePlan را تغییر نمی‌دهد.
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_ReviewRecommendation_NeverModifiesMaintenanceStrategyOrPlan));
        var recommendation = new RcmReviewRecommendation
        {
            EquipmentId = equipmentId, MaintenanceStrategyId = strategyId, TriggerReason = "HighDowntime",
            Evidence = "test evidence", WindowDays = 90, Status = "PENDING", GeneratedDate = DateTime.Now, IsActive = true
        };
        ctx.RcmReviewRecommendations.Add(recommendation);
        await ctx.SaveChangesAsync();

        var strategyBefore = await ctx.MaintenanceStrategies.AsNoTracking().SingleAsync(x => x.MaintenanceStrategyId == strategyId);

        var service = new RcmFeedbackService(ctx);
        await service.ReviewRecommendationAsync(recommendation.RcmReviewRecommendationId, accepted: true, "supervisor1", null);

        var strategyAfter = await ctx.MaintenanceStrategies.AsNoTracking().SingleAsync(x => x.MaintenanceStrategyId == strategyId);
        Assert.Equal(strategyBefore.StrategyType, strategyAfter.StrategyType);
        Assert.Equal(strategyBefore.Justification, strategyAfter.Justification);
        Assert.Empty(ctx.MaintenancePlans); // هیچ Plan ای خودکار ساخته نشد
    }

    [Fact]
    public async Task RcmFeedback_GetRecommendationsAsync_FiltersByStatus()
    {
        var (ctx, equipmentId, strategyId) = await SeedStrategyAsync(nameof(RcmFeedback_GetRecommendationsAsync_FiltersByStatus));
        ctx.RcmReviewRecommendations.AddRange(
            new RcmReviewRecommendation { EquipmentId = equipmentId, MaintenanceStrategyId = strategyId, TriggerReason = "HighDowntime", Evidence = "e1", WindowDays = 90, Status = "PENDING", GeneratedDate = DateTime.Now, IsActive = true },
            new RcmReviewRecommendation { EquipmentId = equipmentId, MaintenanceStrategyId = strategyId, TriggerReason = "HighFailureFrequency", Evidence = "e2", WindowDays = 90, Status = "ACCEPTED", GeneratedDate = DateTime.Now, IsActive = true }
        );
        await ctx.SaveChangesAsync();

        var service = new RcmFeedbackService(ctx);
        var pending = await service.GetRecommendationsAsync("PENDING");

        var only = Assert.Single(pending);
        Assert.Equal("HighDowntime", only.TriggerReason);
    }
}
