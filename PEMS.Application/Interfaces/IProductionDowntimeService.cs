using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۷: رویداد واقعی توقف - جدا از
/// WorkOrder.DowntimeHours/FailureRecord.DowntimeHours (که دست‌نخورده می‌مانند).
/// </summary>
public interface IProductionDowntimeService
{
    Task<List<ProductionDowntimeEventDto>> GetForRunAsync(int productionRunId);

    Task<(bool Success, string? Error, int? ProductionDowntimeEventId)> StartAsync(
        int productionRunId, int productionWorkCenterId, int? equipmentId, DateTime startTime,
        string causeCategory, string? description, int? sourceWorkRequestId, int? sourceWorkOrderId,
        int? createdByEmployeeId);

    Task<(bool Success, string? Error)> CloseAsync(int productionDowntimeEventId, DateTime endTime);
}
