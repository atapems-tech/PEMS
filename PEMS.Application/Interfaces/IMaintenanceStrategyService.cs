using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard»: صفحه‌بندی/فیلتر equipmentId در SQL.
/// </summary>
public interface IMaintenanceStrategyService
{
    Task<PagedResult<MaintenanceStrategyListDto>> GetListAsync(int page = 1, int pageSize = 50, int? equipmentId = null);
}
