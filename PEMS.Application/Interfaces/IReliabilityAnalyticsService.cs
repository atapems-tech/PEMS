using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 5 — Final Reliability Intelligence, Failure Analytics & RAM Foundation» بند
/// ۱۴: موتور مرکزی KPI قابلیت‌اطمینان - سطح کلاس، Pareto، Bad Actor، روند زمانی. روی همون
/// IReliabilityService موجود (فرمول‌های تاییدشده‌ی MTBF/MTTR/Availability) سوار می‌شود - یک
/// موتور محاسباتی موازی نیست.
/// </summary>
public interface IReliabilityAnalyticsService
{
    Task<List<EquipmentClassReliabilityDto>> GetClassReliabilityAsync();
    Task<EquipmentClassReliabilityDto?> GetClassReliabilityAsync(int equipmentClassId);

    Task<List<FailureParetoItemDto>> GetFailureModeParetoAsync(DateTime? from, DateTime? to, int top = 20);
    Task<List<FailureParetoItemDto>> GetFailureCauseParetoAsync(DateTime? from, DateTime? to, int top = 20);

    Task<List<BadActorDto>> GetBadActorsAsync(DateTime? from, DateTime? to, int top = 20);

    Task<List<ReliabilityTrendPointDto>> GetMonthlyTrendAsync(DateTime? from, DateTime? to);
}
