using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md بخش ۴: چرخه‌عمر CMMS یک Equipment - نگهداری، بازرسی،
/// خرابی و هزینه - جدا از هویت مهندسی خودِ Equipment (Tag/Manufacturer/Model).
/// </summary>
public interface IAssetService
{
    Task<PagedResult<AssetListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null, string? q = null);

    Task<AssetDetailDto?> GetDetailsAsync(int assetId);

    Task<AssetSummaryDto?> GetByEquipmentIdAsync(int equipmentId);

    Task<(bool Success, string? Error, int? AssetId)> CreateAsync(
        int equipmentId, string assetNumber, string? functionalLocation, string? maintenanceClass,
        DateTime? commissioningDate, string status, string? description, string? currentUserName,
        int? functionalLocationId = null);

    Task<(bool Success, string? Error)> UpdateAsync(
        int assetId, string assetNumber, string? functionalLocation, string? maintenanceClass,
        DateTime? commissioningDate, string status, string? description, string? currentUserName,
        int? functionalLocationId = null);

    Task ToggleActiveAsync(int assetId, string? currentUserName);
}
