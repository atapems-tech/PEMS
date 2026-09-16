using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۶: فقط CRUD روی هدف - محاسبه‌ی
/// Actual/Variance این‌جا نیست (در IOperationsReportService، از ProductionOutput خوانده می‌شود).
/// </summary>
public interface IProductionTargetService
{
    Task<List<ProductionTargetDto>> GetListAsync(DateTime? fromDate = null, DateTime? toDate = null, int? productionWorkCenterId = null);

    Task<(bool Success, string? Error, int? ProductionTargetId)> CreateAsync(
        DateTime targetDate, int? productionOrderId, int materialItemId, int productionWorkCenterId,
        int? shiftDefinitionId, decimal targetQuantity, int? uomId, int? createdByEmployeeId);

    Task<(bool Success, string? Error)> DeactivateAsync(int productionTargetId);
}
