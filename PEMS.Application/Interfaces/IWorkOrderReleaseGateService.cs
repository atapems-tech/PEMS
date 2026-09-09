using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 6» بند ۱۸: قبل از اجرا (Assigned → InProgress)، کنترل‌های ایمنی موجود
/// (Permit/Isolation/GasTest) واقعاً بررسی می‌شوند - فقط شرط‌هایی که مدل داده‌ی فعلی
/// (JobPlan.PermitRequired/LOTORequired، PermitExecutionRequirement) پشتیبانی می‌کند، نه
/// حدسی جدید.
/// </summary>
public interface IWorkOrderReleaseGateService
{
    Task<WorkOrderReleaseGateResultDto> EvaluateAsync(int workOrderId);
}
