using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IVDRService
{
    Task<List<VDRItemDto>> GetAllAsync(int? projectId = null, int? vendorId = null, string? workflowStatus = null);

    // طبق سند Global Data Grid Standard فاز ۲: نسخه‌ی صفحه‌بندی‌شده‌ی سمت-SQL برای
    // Views/VDR/Index.cshtml - GetAllAsync بالا دست‌نخورده باقی می‌ماند (مصرف‌کننده‌ی دیگری
    // ندارد جز همین Index که حالا به این متد سوییچ می‌کند، اما حذف نشد تا ریسک بی‌ربط ایجاد نشود)
    Task<PagedResult<VDRListItemDto>> GetPagedAsync(
        int? projectId, int? vendorId, string? workflowStatus, int page, int pageSize, string? search = null);

    /// <summary>
    /// SCRUM-409: همه‌ی ردیف‌های منطبق با همان فیلترهای فهرست (بدون صفحه‌بندی) برای خروجی Excel؛ حداکثر <paramref name="maxRows"/> ردیف
    /// برگردانده می‌شود و Total تعداد واقعی منطبق است تا فراخواننده سقف را تشخیص دهد.
    /// </summary>
    Task<(List<VDRListItemDto> Items, int Total)> GetAllMatchingAsync(
        int? projectId, int? vendorId, string? workflowStatus, string? search, int maxRows);

    Task<VDRItemDto?> GetByIdAsync(int id);

    Task<int> CreateManualAsync(VDRItemDto item);

    Task<VDRDashboardDto> GetDashboardAsync(int? projectId = null);
}
