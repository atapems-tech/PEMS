using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Work Request/Work Order Scheduling &amp; Maintenance Calendar»: فقط خواندنی/تجمیعی
/// روی WorkOrder واقعی - هیچ Entity/چرخه‌عمر تازه‌ای اینجا نیست. تخصیص زمان‌بندی خودش از
/// IWorkOrderLifecycleService.ScheduleWorkOrderAsync می‌آید (همان الگوی AssignExecutorAsync).
/// </summary>
public interface ICmmsCalendarService
{
    /// <summary>WorkOrder های با PlannedStart/PlannedFinish هم‌پوشان با بازه، به‌همراه فهرست واجدشرایط بدون زمان‌بندی.</summary>
    Task<MaintenanceCalendarResultDto> GetCalendarAsync(
        DateTime rangeStart, DateTime rangeEnd, int? teamId, int? employeeId,
        string? status, string? workOrderType, string? priority, int? equipmentId,
        bool includeUnscheduled = true);

    /// <summary>کار برنامه‌ریزی‌شده‌ی یک کارمند مشخص در یک روز - برای نمای «کار من».</summary>
    Task<List<WorkOrderCalendarItemDto>> GetMyWorkAsync(int employeeId, DateTime day);

    Task<List<CalendarFilterOptionDto>> GetActiveTeamsAsync();
}
