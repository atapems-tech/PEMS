using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// The reads of the reliability pages as operations (SCRUM-746): each one answers what the service already answers (the DTOs of the analytics, intelligence, RAM, condition monitoring, predictive and AI services),
/// the operation data source turns an answer into rows and a nested list is reached with its <c>path</c>. The permission is the one ReliabilityController checked on every read: Reliability.View. The commands are in
/// IReliabilityOperations; the page that was a POST (the Monte Carlo simulation) is a read with its parameters.
/// </summary>
public interface IReliabilityPagesOperations
{
    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Fleet", "Reliability.View")]
    Task<List<EquipmentReliabilityDto>> FleetAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "ClassReliability", "Reliability.View")]
    Task<List<EquipmentClassReliabilityDto>> ClassReliabilityAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "ClassReliabilityOne", "Reliability.View")]
    Task<EquipmentClassReliabilityDto?> ClassReliabilityOneAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "FailureModePareto", "Reliability.View")]
    Task<List<FailureParetoItemDto>> FailureModeParetoAsync(DateTime? from = null, DateTime? to = null, int top = 20);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "FailureCausePareto", "Reliability.View")]
    Task<List<FailureParetoItemDto>> FailureCauseParetoAsync(DateTime? from = null, DateTime? to = null, int top = 20);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "BadActors", "Reliability.View")]
    Task<List<BadActorDto>> BadActorsAsync(DateTime? from = null, DateTime? to = null, int top = 20);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "MonthlyTrend", "Reliability.View")]
    Task<List<ReliabilityTrendPointDto>> MonthlyTrendAsync(DateTime? from = null, DateTime? to = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Equipment", "Reliability.View")]
    Task<EquipmentReliabilityDto?> EquipmentAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "FailureHistory", "Reliability.View")]
    Task<List<FailureHistoryItemDto>> FailureHistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "DataQuality", "Reliability.View")]
    Task<ReliabilityDataQualityDto> DataQualityAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "PmCompliance", "Reliability.View")]
    Task<PmComplianceDto> PmComplianceAsync(DateTime? from = null, DateTime? to = null, int? equipmentClassId = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "MaintenanceTypes", "Reliability.View")]
    Task<List<MaintenanceTypeItemDto>> MaintenanceTypesAsync(DateTime? from = null, DateTime? to = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "EmergencyWork", "Reliability.View")]
    Task<MaintenanceTypeBreakdownDto> EmergencyWorkAsync(DateTime? from = null, DateTime? to = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "RepeatFailures", "Reliability.View")]
    Task<List<RepeatFailureGroupDto>> RepeatFailuresAsync(DateTime? from = null, DateTime? to = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Effectiveness", "Reliability.View")]
    Task<MaintenanceEffectivenessDto> EffectivenessAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "CriticalityMatrix", "Reliability.View")]
    Task<List<CriticalityMatrixRowDto>> CriticalityMatrixAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "CriticalityMaintenanceMatrix", "Reliability.View")]
    Task<List<CriticalityFailureMaintenanceRowDto>> CriticalityMaintenanceMatrixAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Recommendations", "Reliability.View")]
    Task<List<RcmReviewRecommendationDto>> RecommendationsAsync(string? status = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Ram", "Reliability.View")]
    Task<RamAnalysisDto> RamAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Weibull", "Reliability.View")]
    Task<WeibullAnalysisDto> WeibullAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Simulation", "Reliability.View")]
    Task<ReliabilitySimulationDto> SimulationAsync(int id, int iterations = 1000, double horizonHours = 8760);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "EquipmentIntelligence", "Reliability.View")]
    Task<EquipmentIntelligenceProfileDto?> EquipmentIntelligenceAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "ClassIntelligence", "Reliability.View")]
    Task<ClassIntelligenceProfileDto?> ClassIntelligenceAsync(int id);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "CbmDashboard", "Reliability.View")]
    Task<CbmDashboardDto> CbmDashboardAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "ConditionFoundation", "Reliability.View")]
    Task<ConditionMonitoringFoundationStatusDto> ConditionFoundationAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "ConditionPoints", "Reliability.View")]
    Task<List<ConditionMonitoringPointDto>> ConditionPointsAsync(int equipmentId);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "Trend", "Reliability.View")]
    Task<ConditionMonitoringTrendDto?> TrendAsync(int pointId, string range = "30", DateTime? from = null, DateTime? to = null);

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "PredictiveReadiness", "Reliability.View")]
    Task<PredictiveMaintenanceReadinessDto> PredictiveReadinessAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "PredictiveModels", "Reliability.View")]
    Task<List<PredictiveMaintenanceModelDto>> PredictiveModelsAsync();

    [PEMS.Application.Common.PemsOperation("ReliabilityPage", "AiInsights", "Reliability.View")]
    Task<List<AIReliabilityInsightDto>> AiInsightsAsync(string? status = null);
}
