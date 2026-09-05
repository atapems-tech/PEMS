using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-08 (Cost &amp; KPI).
/// </summary>
public interface IKpiService
{
    Task<CostKpiDto> GetCostKpisAsync(DateTime? fromDate, DateTime? toDate);

    Task<MaintenanceKpiDto> GetMaintenanceKpisAsync(DateTime? fromDate, DateTime? toDate);
}
