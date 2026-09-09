namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 6» بند ۱۳/۵۳: نتیجه‌ی اعمال یک RCMDecision تاییدشده روی یک Equipment مشخص.
/// هرگز خودکار اجرا نمی‌شود - فقط وقتی یک کاربر مجاز صریحاً این عملیات را فراخوانی کند.
/// </summary>
public class ApplyRcmDecisionResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public int? MaintenanceStrategyId { get; set; }
    public bool MaintenanceStrategyWasReused { get; set; }

    public int? MaintenancePlanId { get; set; }
    public bool MaintenancePlanWasReused { get; set; }
}
