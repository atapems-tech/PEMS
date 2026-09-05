namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md بخش ۸ (Work Order)، ۱۰ (Failure) و ۱۲ (Maintenance History):
/// جابجایی وضعیت WorkOrder به همراه اثرات جانبی‌اش - ثبت تاریخچه‌ی تغییر وضعیت، همگام‌سازی
/// FailureRecord از روی فیلدهای ISO-14224 خودِ WorkOrder (به‌جای دو منبع حقیقت مستقل)، و
/// ساخت خودکار MaintenanceHistory از روی WorkOrder تکمیل‌شده (نه یک Entity یتیم بدون نویسنده).
/// </summary>
public interface IWorkOrderLifecycleService
{
    Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int workOrderId, string nextStatus, string? reason, int? changedByEmployeeId,
        int? assignedTeamId, int? assignedEmployeeId,
        string? failureModeCode, string? failureCauseCode, string? rootCause, decimal? downtimeHours,
        string? failureEffect = null, string? correctiveAction = null,
        decimal? contractorCost = null, string? contractorName = null);
}
