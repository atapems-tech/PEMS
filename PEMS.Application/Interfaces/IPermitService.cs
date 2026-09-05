using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS-10a Execution Baseline بند ۱۱: گذار وضعیت Permit باید صریح و کنترل‌شده باشد -
/// نه یک انتساب آزاد به permit.Status از یک Controller نامرتبط. اعتبارسنجی گذار داخل Service
/// انجام می‌شود، همون الگوی WorkRequestService/WorkOrderLifecycleService.
/// </summary>
public interface IPermitService
{
    Task<List<PermitListDto>> GetListAsync(string? status = null);

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): مسیر pendingMyApproval باید
    // با GetListAsync کامل کار کند (چون فیلتر آن روی IWorkflowRuntime است، نه ستونی از DB که
    // بشود در SQL فیلتر کرد) - اما مسیر معمولی (بدون pendingMyApproval) باید واقعاً در DB
    // صفحه‌بندی شود.
    Task<PagedResult<PermitListDto>> GetPagedListAsync(string? status, int page, int pageSize);

    Task<PermitDetailDto?> GetDetailsAsync(int permitId);

    Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string permitType, string title, string? description, int requestedByEmployeeId,
        int? equipmentId, int? functionalLocationId, int? workOrderId, int? permitReceiverEmployeeId,
        string priority, DateTime? validFrom, DateTime? validTo);

    Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int permitId, string nextStatus, string? reason, int? changedByEmployeeId);

    // ==============================================================================
    // طبق CMMS-10b: زنجیره‌ی تایید واقعی چندنقشی از طریق IWorkflowRuntime موجود (نه یک
    // موتور موازی). این متدها فقط گذار Permit.Status از UnderReview→Approved/Rejected/Draft
    // را کنترل می‌کنند - بقیه‌ی گذارها (Submit/Issue/Suspend/...) دست‌نخورده باقی می‌مانند.
    // ==============================================================================

    Task<(bool Success, string? Error)> SubmitForApprovalAsync(int permitId, int? actorEmployeeId);

    Task<(bool Success, string? Error)> RespondApprovalAsync(
        int permitId, string decision, string? comment, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress, string? userAgent);

    Task<(bool Success, string? Error)> ReturnForReworkAsync(
        int permitId, string? comment, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress, string? userAgent);

    Task<bool> HasPendingApprovalAsync(int permitId);
}
