using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 7.5 — CMMS Operational Data Readiness & Master Data Activation»: آیا PEMS
/// می‌تواند با داده‌ی مرجع موجود، واقعاً روی یک تجهیز واقعی کار نگهداری اجرا کند؟ این سرویس فقط
/// می‌خواند و گزارش می‌دهد - هرگز خودش MaintenanceStrategy/MaintenancePlan/JobPlan/Criticality
/// نمی‌سازد (بند ۴۳: ممنوعیت فعال‌سازی خودکار). فعال‌سازی واقعی همچنان از طریق کنترلرهای
/// موجود (MaintenanceStrategyController/JobPlanController/EquipmentCriticalityController،
/// اکنون با مجوز درست) انجام می‌شود.
/// </summary>
public interface IEquipmentReadinessService
{
    Task<CmmsOperationalReadinessDto> GetOperationalReadinessSummaryAsync();

    Task<List<ClassActivationBacklogItemDto>> GetClassActivationBacklogAsync();

    Task<PagedResult<EquipmentReadinessRowDto>> GetEquipmentReadinessAsync(int? equipmentClassId, int page = 1, int pageSize = 50);
}
