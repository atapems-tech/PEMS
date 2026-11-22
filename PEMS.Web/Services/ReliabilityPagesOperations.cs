using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-746: the reads of the reliability pages over the same services the controller called (see <see cref="IReliabilityPagesOperations"/>).</summary>
public sealed class ReliabilityPagesOperations(IReliabilityService reliability, IReliabilityAnalyticsService analytics, IReliabilityDataQualityService dataQuality, IReliabilityIntelligenceService intelligence,
    IRcmFeedbackService rcmFeedback, IRamWeibullService ramWeibull, IReliabilitySimulationService simulation, IConditionMonitoringService monitoring, IPredictiveMaintenanceService predictive,
    IAIReliabilityInsightService aiInsights, IClassIntelligenceService classIntelligence, IEquipmentIntelligenceService equipmentIntelligence) : IReliabilityPagesOperations
{
    public Task<List<EquipmentReliabilityDto>> FleetAsync() => reliability.GetFleetReliabilityAsync();
    public Task<List<EquipmentClassReliabilityDto>> ClassReliabilityAsync() => analytics.GetClassReliabilityAsync();
    public Task<EquipmentClassReliabilityDto?> ClassReliabilityOneAsync(int id) => analytics.GetClassReliabilityAsync(id);
    public Task<List<FailureParetoItemDto>> FailureModeParetoAsync(DateTime? from = null, DateTime? to = null, int top = 20) => analytics.GetFailureModeParetoAsync(from, to, top);
    public Task<List<FailureParetoItemDto>> FailureCauseParetoAsync(DateTime? from = null, DateTime? to = null, int top = 20) => analytics.GetFailureCauseParetoAsync(from, to, top);
    public Task<List<BadActorDto>> BadActorsAsync(DateTime? from = null, DateTime? to = null, int top = 20) => analytics.GetBadActorsAsync(from, to, top);
    public Task<List<ReliabilityTrendPointDto>> MonthlyTrendAsync(DateTime? from = null, DateTime? to = null) => analytics.GetMonthlyTrendAsync(from, to);
    public Task<EquipmentReliabilityDto?> EquipmentAsync(int id) => reliability.GetEquipmentReliabilityAsync(id);
    public async Task<List<FailureHistoryItemDto>> FailureHistoryAsync(int id) => (await reliability.GetEquipmentReliabilityAsync(id))?.FailureHistory ?? [];
    public Task<ReliabilityDataQualityDto> DataQualityAsync() => dataQuality.GetDataQualityAsync();
    public Task<PmComplianceDto> PmComplianceAsync(DateTime? from = null, DateTime? to = null, int? equipmentClassId = null) => intelligence.GetPmComplianceAsync(from, to, equipmentClassId);
    public async Task<List<MaintenanceTypeItemDto>> MaintenanceTypesAsync(DateTime? from = null, DateTime? to = null) => (await intelligence.GetMaintenanceTypeBreakdownAsync(from, to)).Items;
    public Task<MaintenanceTypeBreakdownDto> EmergencyWorkAsync(DateTime? from = null, DateTime? to = null) => intelligence.GetMaintenanceTypeBreakdownAsync(from, to);
    public Task<List<RepeatFailureGroupDto>> RepeatFailuresAsync(DateTime? from = null, DateTime? to = null) => intelligence.GetRepeatFailuresAsync(from, to);
    public Task<MaintenanceEffectivenessDto> EffectivenessAsync() => intelligence.GetMaintenanceEffectivenessAsync();
    public Task<List<CriticalityMatrixRowDto>> CriticalityMatrixAsync() => intelligence.GetCriticalityFailureMatrixAsync();
    public Task<List<CriticalityFailureMaintenanceRowDto>> CriticalityMaintenanceMatrixAsync() => intelligence.GetCriticalityFailureMaintenanceMatrixAsync();
    public Task<List<RcmReviewRecommendationDto>> RecommendationsAsync(string? status = null) => rcmFeedback.GetRecommendationsAsync(string.IsNullOrWhiteSpace(status) ? null : status);
    public Task<RamAnalysisDto> RamAsync(int id) => ramWeibull.GetRamAnalysisAsync(id);
    public Task<WeibullAnalysisDto> WeibullAsync(int id) => ramWeibull.GetWeibullAnalysisAsync(id);
    public Task<ReliabilitySimulationDto> SimulationAsync(int id, int iterations = 1000, double horizonHours = 8760) => simulation.SimulateAvailabilityAsync(id, iterations, horizonHours);
    public Task<EquipmentIntelligenceProfileDto?> EquipmentIntelligenceAsync(int id) => equipmentIntelligence.GetProfileAsync(id);
    public Task<ClassIntelligenceProfileDto?> ClassIntelligenceAsync(int id) => classIntelligence.GetClassIntelligenceAsync(id);
    public Task<CbmDashboardDto> CbmDashboardAsync() => monitoring.GetCbmDashboardAsync();
    public Task<ConditionMonitoringFoundationStatusDto> ConditionFoundationAsync() => monitoring.GetFoundationStatusAsync();
    public Task<List<ConditionMonitoringPointDto>> ConditionPointsAsync(int equipmentId) => monitoring.GetPointsForEquipmentAsync(equipmentId);

    // the range of the trend is always bounded: a preset (7, 30, 90, 365 days) or a custom range of at most two years, never the whole history
    public Task<ConditionMonitoringTrendDto?> TrendAsync(int pointId, string range = "30", DateTime? from = null, DateTime? to = null)
    {
        DateTime fromDate, toDate;
        if (range == "custom" && from.HasValue && to.HasValue)
        {
            fromDate = from.Value.Date;
            toDate = to.Value.Date.AddDays(1).AddTicks(-1);
            if ((toDate - fromDate).TotalDays > 730) fromDate = toDate.AddDays(-730);
        }
        else
        {
            var days = range switch { "7" => 7, "90" => 90, "365" => 365, _ => 30 };
            toDate = DateTime.Now;
            fromDate = toDate.AddDays(-days);
        }
        return monitoring.GetTrendAsync(pointId, fromDate, toDate);
    }

    public Task<PredictiveMaintenanceReadinessDto> PredictiveReadinessAsync() => predictive.GetReadinessAsync();
    public Task<List<PredictiveMaintenanceModelDto>> PredictiveModelsAsync() => predictive.GetModelsAsync();
    public Task<List<AIReliabilityInsightDto>> AiInsightsAsync(string? status = null) => aiInsights.GetInsightsAsync(string.IsNullOrWhiteSpace(status) ? null : status);
}
