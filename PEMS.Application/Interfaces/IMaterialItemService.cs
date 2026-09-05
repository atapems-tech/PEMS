using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IMaterialItemService
{
    Task<PagedResult<MaterialItemListDto>> GetListAsync(
        int page = 1, int pageSize = 25, string? q = null, int? categoryId = null,
        bool lowStockOnly = false, bool criticalOnly = false);

    Task<MaterialItemDetailDto?> GetDetailsAsync(int materialItemId);

    Task<(bool Success, string? Error, int? MaterialItemId)> CreateAsync(
        string materialCode, string description, string? materialType, string? longDescription,
        int? categoryId, int? disciplineId, int? uomId, string? manufacturer, string? manufacturerPartNumber,
        int? vendorId, string? specification, string? model, string? size, string? materialGrade,
        bool isSerialized, bool isBatchControlled, bool isStockItem, bool isCritical, string? currentUserName);

    Task<(bool Success, string? Error)> UpdateAsync(
        int materialItemId, string materialCode, string description, string? materialType, string? longDescription,
        int? categoryId, int? disciplineId, int? uomId, string? manufacturer, string? manufacturerPartNumber,
        int? vendorId, string? specification, string? model, string? size, string? materialGrade,
        bool isSerialized, bool isBatchControlled, bool isStockItem, bool isCritical, string? currentUserName);

    Task ToggleActiveAsync(int materialItemId, string? currentUserName);
}
