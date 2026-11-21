namespace PEMS.Application.Interfaces;

/// <summary>
/// Write commands of the equipment criticality, equipment and relationship controllers as operations (SCRUM-692, 693, 745): the criticality assessment (one active analysis per equipment, the previous one is
/// replaced), the placement of an equipment (functional location, parent, installation date) and the confirmation of a suggested equipment link. The permissions are the ones the controllers checked.
/// </summary>
public interface IReliabilityRegisterOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentCriticality", "Assess", "EquipmentCriticality.Create")]
    Task<(bool Success, string? Error)> AssessCriticalityAsync(int equipmentId, string criticalityClass, string? riskLevel = null, string? safetyImpact = null, string? productionImpact = null,
        string? environmentImpact = null, string? description = null);

    [PEMS.Application.Common.PemsOperation("Equipment", "AssignFoundation", "Equipment.Edit")]
    Task<(bool Success, string? Error)> AssignFoundationAsync(int equipmentId, int? functionalLocationId = null, int? parentEquipmentId = null, DateTime? installationDate = null);

    [PEMS.Application.Common.PemsOperation("Relationship", "ConfirmSuggestedLink", "Relationship.Approve")]
    Task<(bool Success, string? Error)> ConfirmSuggestedLinkAsync(int equipmentId, string targetType, int targetId, bool force = false);
}
