using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-05: مسیر رسمی قبل از ساخت WorkOrder اجرایی -
/// Draft → Submitted → UnderReview → Approved → {Rejected | Planned → Converted}.
/// </summary>
public interface IWorkRequestService
{
    Task<PagedResult<WorkRequestListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null);

    Task<WorkRequestDetailDto?> GetDetailsAsync(int workRequestId);

    Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string title, string? description, int requestedById, int? equipmentId, int? assetId,
        int? functionalLocationId, string? reportedCondition, string priority, int? suggestedDisciplineId);

    /// <summary>
    /// گذار وضعیت را طبق جدول داخلی مجاز بودن اعتبارسنجی می‌کند - قبل از هرگونه تغییر، نه
    /// فقط با اعتماد به مقدار ارسالی از Controller (طبق بند ۹.۳ سند Baseline).
    /// </summary>
    Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int workRequestId, string nextStatus, string? reason, int? changedByEmployeeId);

    Task<(bool Success, string? Error, int? WorkOrderId)> ConvertToWorkOrderAsync(
        int workRequestId, string workOrderType, string? currentUserName);
}
