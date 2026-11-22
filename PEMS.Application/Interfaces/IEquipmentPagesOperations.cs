using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public sealed record EquipmentChoice(string Value, string Label);

/// <summary>
/// The equipment list and the digital equipment passport as operations (SCRUM-692): the list (the service page of all equipment, the grid pages it in the browser), the passport (the service answer; the data sources reach its parts with their
/// <c>path</c>: Equipment, Documents, VDRItems, RelatedMRs, WorkOrders, WorkRequests, Strategies, Plans, NonConformanceReports, MOCs, TechnicalQueries, SuggestedLinks, Instrument, Asset, MechanicalEquipment, ConditionMonitoring), the
/// inspections, the pending classification suggestion and its explanation, the maintenance intelligence and the choices of the create form. EquipmentController checked no permission on its reads, so the operations carry none (signed-in
/// users only, as before); the create is an entity action with the permission the controller checked (Equipment.Create).
/// </summary>
public interface IEquipmentPagesOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentPage", "List")]
    Task<List<EquipmentDto>> ListAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "Passport")]
    Task<EquipmentPassportDto?> PassportAsync(int id);

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "Inspections")]
    Task<List<InspectionListItemDto>> InspectionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "ClassificationSuggestion")]
    Task<ReviewQueueItemDto?> ClassificationSuggestionAsync(int id);

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "ClassificationExplanation")]
    Task<ClassificationExplanationDto?> ClassificationExplanationAsync(int id);

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "MaintenanceIntelligence")]
    Task<EquipmentMaintenanceIntelligenceDto?> MaintenanceIntelligenceAsync(int id);

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "ProjectOptions")]
    Task<List<EquipmentChoice>> ProjectOptionsAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentPage", "DisciplineOptions")]
    Task<List<EquipmentChoice>> DisciplineOptionsAsync();
}
