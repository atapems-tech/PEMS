using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-670, 691, 700: the write commands of the asset, functional location and classification controllers (see <see cref="IAssetRegisterOperations"/>).</summary>
public sealed class AssetRegisterOperations(IAssetService assets, IFunctionalLocationService locations, IEquipmentClassificationService classification, ICurrentUserService currentUser) : IAssetRegisterOperations
{
    public async Task<(bool Success, string? Error)> CreateLocationAsync(string code, string name, int? parentId = null, string? plant = null, string? area = null, string? unit = null, string? description = null)
    {
        var (success, error, _) = await locations.CreateAsync(code, name, parentId, plant, area, unit, description, currentUser.UserName);
        return (success, error);
    }

    public Task<(bool Success, string? Error)> EditLocationAsync(int id, string code, string name, int? parentId = null, string? plant = null, string? area = null, string? unit = null, string? description = null)
        => locations.UpdateAsync(id, code, name, parentId, plant, area, unit, description, currentUser.UserName);

    public async Task<(bool Success, string? Error)> CreateAssetAsync(int equipmentId, string assetNumber, string status, string? functionalLocation = null, string? maintenanceClass = null,
        DateTime? commissioningDate = null, string? description = null, int? functionalLocationId = null)
    {
        var (success, error, _) = await assets.CreateAsync(equipmentId, assetNumber, functionalLocation, maintenanceClass, commissioningDate, status, description, currentUser.UserName, functionalLocationId);
        return (success, error);
    }

    public Task<(bool Success, string? Error)> EditAssetAsync(int assetId, string assetNumber, string status, string? functionalLocation = null, string? maintenanceClass = null,
        DateTime? commissioningDate = null, string? description = null, int? functionalLocationId = null)
        => assets.UpdateAsync(assetId, assetNumber, functionalLocation, maintenanceClass, commissioningDate, status, description, currentUser.UserName, functionalLocationId);

    public async Task<(bool Success, string? Error)> ToggleAssetAsync(int id)
    {
        await assets.ToggleActiveAsync(id, currentUser.UserName);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AcceptSuggestionAsync(int id, string? comment = null)
        => await classification.AcceptSuggestionAsync(id, currentUser.UserName ?? "Unknown", comment) ? (true, null) : (false, "این پیشنهاد یافت نشد یا قبلاً بررسی شده.");

    public async Task<(bool Success, string? Error)> RejectSuggestionAsync(int id, string? comment = null)
        => await classification.RejectSuggestionAsync(id, currentUser.UserName ?? "Unknown", comment) ? (true, null) : (false, "این پیشنهاد یافت نشد یا قبلاً بررسی شده.");
}
