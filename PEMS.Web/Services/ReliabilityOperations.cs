using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-678: the write commands of ReliabilityController, the logic moved without change (see <see cref="IReliabilityOperations"/>).</summary>
public sealed class ReliabilityOperations(PEMSDbContext context, IRcmFeedbackService rcmFeedback, IConditionMonitoringService monitoring, IAIReliabilityInsightService insights, ICurrentUserService currentUser) : IReliabilityOperations
{
    public async Task<(bool Success, string? Error)> GenerateRcmEvidenceAsync()
    {
        await rcmFeedback.GenerateEvidenceAsync();
        return (true, null);
    }

    public Task<(bool Success, string? Error)> ReviewRcmRecommendationAsync(int recommendationId, bool accept, string? notes = null)
        => rcmFeedback.ReviewRecommendationAsync(recommendationId, accept, currentUser.UserName ?? "Unknown", notes);

    public async Task<(bool Success, string? Error)> CreateConditionMonitoringPointAsync(int equipmentId, string variableType, string tag, string? unit = null, decimal? alarmLow = null, decimal? alarmHigh = null, string? description = null)
    {
        var (success, error, _) = await monitoring.CreatePointAsync(equipmentId, variableType, tag, unit, alarmLow, alarmHigh, description, currentUser.UserName);
        return (success, error);
    }

    public Task<(bool Success, string? Error)> RecordConditionReadingAsync(int pointId, DateTime readingDate, decimal value, string source, string? notes = null, int? instrumentId = null)
        => monitoring.RecordReadingAsync(pointId, readingDate, value, source, notes, currentUser.UserName, instrumentId);

    // one-way, like Raise Finding: a reading marked for engineering review is never un-marked
    public Task<(bool Success, string? Error)> MarkRequiresEngineeringReviewAsync(int readingId)
        => monitoring.MarkRequiresEngineeringReviewAsync(readingId, currentUser.UserName);

    public Task<(bool Success, string? Error)> AssignEvaluationProfileAsync(int pointId, int? evaluationProfileId = null)
        => monitoring.AssignEvaluationProfileAsync(pointId, evaluationProfileId);

    public async Task<(bool Success, string? Error)> CreateCbmStrategyAsync(int equipmentId, int conditionMonitoringPointId, string? justification = null)
    {
        if (await context.MaintenanceStrategies.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive && x.ConditionMonitoringPointId == conditionMonitoringPointId))
            return (false, "یک استراتژی CBM فعال از قبل برای این نقطه‌ی پایش وجود دارد.");

        await context.MaintenanceStrategies.AddAsync(new Domain.Entities.MaintenanceStrategy
        {
            EquipmentId = equipmentId, StrategyType = "CBM", ConditionMonitoringPointId = conditionMonitoringPointId, ConsequenceCategory = "Operational",
            Justification = justification, RecommendedTriggerType = "Calendar", IsActive = true, CreatedDate = DateTime.Now
        });
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> GenerateAiInsightsAsync(int? equipmentClassId = null)
    {
        var result = await insights.GenerateInsightsAsync(equipmentClassId, currentUser.UserName);
        return (result.Success, result.Error);
    }

    public Task<(bool Success, string? Error)> ReviewAiInsightAsync(int insightId, bool approve, int? reviewedByEmployeeId, string? notes = null)
        => insights.ReviewInsightAsync(insightId, approve, reviewedByEmployeeId, notes);
}
