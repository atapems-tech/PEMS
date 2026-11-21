namespace PEMS.Application.Interfaces;

/// <summary>
/// The write commands of ReliabilityController as operations (SCRUM-678): the RCM evidence and its review, the condition monitoring points and readings, the CBM strategy and the AI insights with their review.
/// Permissions as the controller checked them on the Reliability module (Edit to generate or record, Approve to review). The page reads and the simulation stay with their pages.
/// </summary>
public interface IReliabilityOperations
{
    [PEMS.Application.Common.PemsOperation("Reliability", "GenerateRcmEvidence", "Reliability.Edit")]
    Task<(bool Success, string? Error)> GenerateRcmEvidenceAsync();

    [PEMS.Application.Common.PemsOperation("Reliability", "ReviewRcmRecommendation", "Reliability.Approve")]
    Task<(bool Success, string? Error)> ReviewRcmRecommendationAsync(int recommendationId, bool accept, string? notes = null);

    [PEMS.Application.Common.PemsOperation("Reliability", "CreateConditionMonitoringPoint", "Reliability.Edit")]
    Task<(bool Success, string? Error)> CreateConditionMonitoringPointAsync(int equipmentId, string variableType, string tag, string? unit = null, decimal? alarmLow = null, decimal? alarmHigh = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("Reliability", "RecordConditionReading", "Reliability.Edit")]
    Task<(bool Success, string? Error)> RecordConditionReadingAsync(int pointId, DateTime readingDate, decimal value, string source, string? notes = null, int? instrumentId = null);

    [PEMS.Application.Common.PemsOperation("Reliability", "MarkRequiresEngineeringReview", "Reliability.Edit")]
    Task<(bool Success, string? Error)> MarkRequiresEngineeringReviewAsync(int readingId);

    [PEMS.Application.Common.PemsOperation("Reliability", "AssignEvaluationProfile", "Reliability.Edit")]
    Task<(bool Success, string? Error)> AssignEvaluationProfileAsync(int pointId, int? evaluationProfileId = null);

    [PEMS.Application.Common.PemsOperation("Reliability", "CreateCbmStrategy", "Reliability.Edit")]
    Task<(bool Success, string? Error)> CreateCbmStrategyAsync(int equipmentId, int conditionMonitoringPointId, string? justification = null);

    [PEMS.Application.Common.PemsOperation("Reliability", "GenerateAiInsights", "Reliability.Edit")]
    Task<(bool Success, string? Error)> GenerateAiInsightsAsync(int? equipmentClassId = null);

    [PEMS.Application.Common.PemsOperation("Reliability", "ReviewAiInsight", "Reliability.Approve")]
    Task<(bool Success, string? Error)> ReviewAiInsightAsync(int insightId, bool approve, int? reviewedByEmployeeId, string? notes = null);
}
