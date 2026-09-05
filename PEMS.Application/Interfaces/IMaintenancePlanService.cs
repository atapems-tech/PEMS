using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard»: صفحه‌بندی/مرتب‌سازی در SQL. این Grid قبلاً
/// هیچ فیلتری نداشت (نه q، نه Status) - این مهاجرت فیلتر جدیدی اضافه نمی‌کند، فقط
/// Skip/Take/Select را جایگزین ToListAsync بدون صفحه‌بندی می‌کند.
/// </summary>
public interface IMaintenancePlanService
{
    Task<PagedResult<MaintenancePlanListDto>> GetListAsync(int page = 1, int pageSize = 50);
}
