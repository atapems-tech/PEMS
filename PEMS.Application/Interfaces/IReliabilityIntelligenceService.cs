using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 7 — Maintenance Intelligence & Reliability Operationalization» بند ۵/Step 4:
/// موتور هوشمندی نگهداری - مکمل IReliabilityAnalyticsService موجود (که MTBF/MTTR/Pareto/
/// BadActor/Trend را می‌پوشاند)، نه جایگزین آن. این سرویس دقیقاً همان شکاف‌های واقعی فاز ۷ را
/// می‌پوشاند که در ممیزی اولیه هیچ‌کدام موجود نبودند: PM Compliance واقعی (سررسید-به-سررسید،
/// نه فقط وضعیت لحظه‌ای)، شکست Corrective/Preventive/Emergency با معناشناسی Null، خرابی
/// تکراری، اثربخشی نگهداری چندبعدی، و ماتریس بحرانی×خرابی×نگهداری. همه فقط‌خواندنی و
/// SQL-Aggregated.
///
/// طبق ممیزی: نام "IMaintenanceIntelligenceService" از قبل توسط یک سرویس کاملاً متفاوت فاز ۲/۳
/// (توصیه‌ی Activity/PMTemplate/JobPlan بر اساس سلسله‌مراتب EquipmentClass یک Equipment، در
/// PEMS.Application/Interfaces/IMaintenanceIntelligenceService.cs) اشغال شده بود - این‌جا عمداً
/// یک نام متفاوت (هم‌خانواده‌ی IReliabilityAnalyticsService/IReliabilityDataQualityService
/// موجود) انتخاب شد تا هرگز آن سرویس موجود را رونویسی/تکرار نکند.
/// </summary>
public interface IReliabilityIntelligenceService
{
    Task<PmComplianceDto> GetPmComplianceAsync(DateTime? from, DateTime? to, int? equipmentClassId = null, int? disciplineId = null);

    Task<MaintenanceTypeBreakdownDto> GetMaintenanceTypeBreakdownAsync(DateTime? from, DateTime? to);

    Task<List<RepeatFailureGroupDto>> GetRepeatFailuresAsync(DateTime? from, DateTime? to, int windowDays = 90);

    Task<MaintenanceEffectivenessDto> GetMaintenanceEffectivenessAsync(int windowDays = 90);

    Task<List<CriticalityMatrixRowDto>> GetCriticalityFailureMatrixAsync();
}
