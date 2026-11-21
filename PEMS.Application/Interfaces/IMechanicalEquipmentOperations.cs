namespace PEMS.Application.Interfaces;

/// <summary>
/// The write commands of MechanicalEquipmentController as operations (SCRUM-727): edit the mechanical profile, classify it as rotating or static (exactly one of the two, checked here, not by a database rule) and switch it
/// active or inactive. The controller checked no module permission, so the operations carry none (signed-in users only, as before). The create, which redirects to the new passport, stays in the controller.
/// </summary>
public interface IMechanicalEquipmentOperations
{
    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "Edit")]
    Task<(bool Success, string? Error)> EditAsync(int mechanicalEquipmentId, string equipmentType, decimal? designPressure = null, decimal? designTemperature = null, decimal? operatingPressure = null,
        decimal? operatingTemperature = null, string? material = null, string? manufacturer = null, string? model = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "ClassifyRotating")]
    Task<(bool Success, string? Error)> ClassifyRotatingAsync(int mechanicalEquipmentId, string equipmentType, decimal? flowRate = null, decimal? head = null, int? speedRPM = null, decimal? powerKW = null,
        string? driverType = null, string? bearingType = null, string? lubricationType = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "ClassifyStatic")]
    Task<(bool Success, string? Error)> ClassifyStaticAsync(int mechanicalEquipmentId, string equipmentType, decimal? volume = null, decimal? designPressure = null, decimal? designTemperature = null,
        string? material = null, string? orientation = null, string? codeStandard = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("MechanicalEquipment", "ToggleActive")]
    Task<(bool Success, string? Error)> ToggleActiveAsync(int id);
}
