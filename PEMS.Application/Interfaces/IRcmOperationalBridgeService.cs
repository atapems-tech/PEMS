using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 6» بند ۱۳/۵۳-۵۴: پل کنترل‌شده بین توصیه‌ی سطح-کلاس RCM (فاز ۴) و پیکربندی
/// عملیاتی سطح-Equipment (MaintenanceStrategy/MaintenancePlan). هرگز خودکار اجرا نمی‌شود - فقط
/// روی یک RCMDecision با Status=APPROVED و فقط با فراخوانی صریح یک کاربر مجاز (بند ۲۰: تایید
/// انسانی روی خودِ توصیه کافی نیست، اعمال آن هم یک اقدام آگاهانه‌ی جداست).
/// </summary>
public interface IRcmOperationalBridgeService
{
    Task<ApplyRcmDecisionResultDto> ApplyApprovedDecisionAsync(int rcmDecisionId, int equipmentId, string appliedBy);
}
