namespace PEMS.Application.Interfaces;

public sealed record AssetListRow(int Id, string AssetNumber, string EquipmentTag, string EquipmentName, string MaintenanceClass, string Status, DateTime? CommissioningDate, string CriticalityClass, int OpenWorkOrderCount, string FunctionalLocation);
public sealed record AssetHeadRow(int Id, string AssetNumber, string EquipmentTag, string EquipmentName, string ManufacturerModel, string FunctionalLocation, string FunctionalLocationText, string MaintenanceClass, DateTime? CommissioningDate,
    string Status, string CriticalityClass, string RiskLevel, bool IsActive, string Description, int OpenWorkOrders, decimal TotalCost, DateTime? LatestMaintenanceDate, string LatestAction, string LatestTechnician, string LatestDuration);
public sealed record AssetWorkOrderRow(int Id, string WorkOrderNo, string Description, string Status, DateTime? CreatedDate);
public sealed record AssetInspectionRow(int Id, string InspectionType, DateTime? InspectionDate, string Inspector, string Result);
public sealed record AssetFailureRow(int Id, DateTime FailureDate, string FailureMode, string FailureCause, string DowntimeHours);
public sealed record LocationRow(int Id, string Code, string Name, string Plant, string Area, string Unit, string Parent, string Path, int EquipmentCount, int AssetCount, bool IsActive);
public sealed record PickerOption(string Value, string Label);

/// <summary>
/// The asset pages and the functional location list as operations (SCRUM-670, 700): the list, the passport head and its work orders, inspections and failures, the choice of locations, the location list (reads, signed-in users for assets, FunctionalLocation.View
/// for locations, as the controllers checked) and the validation the location edit runs before the entity write (unique code, no own parent, no cycle). Create and edit of an asset and of a location are entity definitions.
/// </summary>
public interface IAssetPagesOperations
{
    [PEMS.Application.Common.PemsOperation("Asset", "List")]
    Task<List<AssetListRow>> AssetListAsync();

    [PEMS.Application.Common.PemsOperation("Asset", "Head")]
    Task<List<AssetHeadRow>> AssetHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("Asset", "WorkOrders")]
    Task<List<AssetWorkOrderRow>> AssetWorkOrdersAsync(int id);

    [PEMS.Application.Common.PemsOperation("Asset", "Inspections")]
    Task<List<AssetInspectionRow>> AssetInspectionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("Asset", "Failures")]
    Task<List<AssetFailureRow>> AssetFailuresAsync(int id);

    [PEMS.Application.Common.PemsOperation("Asset", "LocationOptions")]
    Task<List<PickerOption>> LocationOptionsAsync();

    [PEMS.Application.Common.PemsOperation("FunctionalLocation", "List", "FunctionalLocation.View")]
    Task<List<LocationRow>> LocationListAsync();

    [PEMS.Application.Common.PemsOperation("FunctionalLocation", "Validate", "FunctionalLocation.Edit")]
    Task<(bool Success, string? Error)> ValidateLocationAsync(int? id, string code, int? parentId = null);
}
