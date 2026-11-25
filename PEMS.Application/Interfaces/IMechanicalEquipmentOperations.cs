namespace PEMS.Application.Interfaces;

public sealed record MechanicalCountsRow(int Total, int Rotating, int Static, int Unclassified);

public sealed record MechanicalEquipmentHeadRow(int Id, string TagNumber, string EquipmentType, string EquipmentTag, string Manufacturer, string Model, string Material, string DesignPressure, string DesignTemperature,
    string OperatingPressure, string OperatingTemperature, string Description, bool IsActive, string SubType);

public sealed record RotatingEquipmentRow(int Id, string EquipmentType, string FlowRate, string Head, string SpeedRPM, string PowerKW, string DriverType, string BearingType, string LubricationType, string Description);

public sealed record StaticEquipmentRow(int Id, string EquipmentType, string Volume, string DesignPressure, string DesignTemperature, string Material, string Orientation, string CodeStandard, string Description);

/// <summary>
/// The mechanical equipment passport as operations (SCRUM-727): the passport head and its rotating or static sub-class (View), the classification as rotating or static (exactly one of the two, checked here, not by a database rule)
/// and the active switch. The list, the create and the edit are entity definitions. The controller checked no module permission, so the operations carry none (signed-in users only, as before).
/// </summary>
public interface IMechanicalEquipmentOperations
{
    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "Counts")]
    Task<List<MechanicalCountsRow>> CountsAsync();

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "Head")]
    Task<List<MechanicalEquipmentHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "Rotating")]
    Task<List<RotatingEquipmentRow>> RotatingAsync(int id);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "Static")]
    Task<List<StaticEquipmentRow>> StaticAsync(int id);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "ClassifyRotating")]
    Task<(bool Success, string? Error)> ClassifyRotatingAsync(int mechanicalEquipmentId, string equipmentType, decimal? flowRate = null, decimal? head = null, int? speedRPM = null, decimal? powerKW = null,
        string? driverType = null, string? bearingType = null, string? lubricationType = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "ClassifyStatic")]
    Task<(bool Success, string? Error)> ClassifyStaticAsync(int mechanicalEquipmentId, string equipmentType, decimal? volume = null, decimal? designPressure = null, decimal? designTemperature = null,
        string? material = null, string? orientation = null, string? codeStandard = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "ToggleActive")]
    Task<(bool Success, string? Error)> ToggleActiveAsync(int id);
}
