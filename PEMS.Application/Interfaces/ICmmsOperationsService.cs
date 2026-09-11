using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 8» بند ۲۱/۲۲: داشبورد عملیات CMMS + Backlog نگهداری - فقط تجمیع/فهرست
/// فقط‌خواندنی روی WorkRequest/WorkOrder موجود؛ هیچ منطق چرخه‌ی عمر جدیدی اینجا نیست
/// (آن در IWorkOrderLifecycleService/WorkRequestService باقی می‌ماند).
/// </summary>
public interface ICmmsOperationsService
{
    Task<CmmsOperationsDashboardDto> GetDashboardAsync();

    Task<PagedResult<MaintenanceBacklogItemDto>> GetBacklogAsync(
        string? status = null, string? priority = null, int? equipmentId = null,
        bool overdueOnly = false, int page = 1, int? pageSize = null);
}
