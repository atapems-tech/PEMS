namespace PEMS.Application.Interfaces;

/// <summary>
/// The write commands of AssetController, FunctionalLocationController and EquipmentClassificationController as operations (SCRUM-670, 691, 700): the same services, the same permissions the controllers
/// checked (now the operation permission), the current user as the actor. Reads and details stay with their pages.
/// </summary>
public interface IAssetRegisterOperations
{
    [PEMS.Application.Common.PemsOperation("FunctionalLocation", "Create", "FunctionalLocation.Create")]
    Task<(bool Success, string? Error)> CreateLocationAsync(string code, string name, int? parentId = null, string? plant = null, string? area = null, string? unit = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("FunctionalLocation", "Edit", "FunctionalLocation.Edit")]
    Task<(bool Success, string? Error)> EditLocationAsync(int id, string code, string name, int? parentId = null, string? plant = null, string? area = null, string? unit = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("Asset", "Create")]
    Task<(bool Success, string? Error)> CreateAssetAsync(int equipmentId, string assetNumber, string status, string? functionalLocation = null, string? maintenanceClass = null,
        DateTime? commissioningDate = null, string? description = null, int? functionalLocationId = null);

    [PEMS.Application.Common.PemsOperation("Asset", "Edit")]
    Task<(bool Success, string? Error)> EditAssetAsync(int assetId, string assetNumber, string status, string? functionalLocation = null, string? maintenanceClass = null,
        DateTime? commissioningDate = null, string? description = null, int? functionalLocationId = null);

    [PEMS.Application.Common.PemsOperation("Asset", "ToggleActive")]
    Task<(bool Success, string? Error)> ToggleAssetAsync(int id);

    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "Accept", "MaintenanceMasterData.Edit")]
    Task<(bool Success, string? Error)> AcceptSuggestionAsync(int id, string? comment = null);

    [PEMS.Application.Common.PemsOperation("EquipmentClassification", "Reject", "MaintenanceMasterData.Edit")]
    Task<(bool Success, string? Error)> RejectSuggestionAsync(int id, string? comment = null);
}
