using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Master Mechanical Equipment List - طبق سند «PEMS — Global Data Grid Standard» و دقیقاً همون
/// الگوی IInstrumentService: صفحه‌بندی/فیلتر/مرتب‌سازی همه در SQL.
/// </summary>
public interface IMechanicalEquipmentService
{
    Task<PagedResult<MechanicalEquipmentListDto>> GetListAsync(int page = 1, int pageSize = 50, string? subType = null, string? q = null);

    Task<MechanicalEquipmentDashboardDto> GetDashboardAsync(string? q = null);
}
