namespace PEMS.Application.Interfaces;

public sealed record FailureModeCatalogRow(int FailureModeId, string Code, string Name, string? NameFa, string? EquipmentClassName, string Causes);
public sealed record FailureModeFormRow(int FailureModeId, string Code, string Name, string? NameFa, string? Description, int? EquipmentClassId, string CauseIds, bool IsActive);
public sealed record FailureModeChoice(string Value, string Label);

/// <summary>
/// The failure mode catalogue list, the saved values of its edit form and the choices of the form as operations (SCRUM-698). FailureModeCatalogController checked MaintenanceMasterData.View on the list and
/// the same module on the form (Create / Edit, kept by the write operations), so every read carries MaintenanceMasterData.View.
/// </summary>
public interface IFailureModeCatalogPagesOperations
{
    [PEMS.Application.Common.PemsOperation("FailureModePage", "List", "MaintenanceMasterData.View")]
    Task<List<FailureModeCatalogRow>> ListAsync(int? classId = null, string? q = null);

    [PEMS.Application.Common.PemsOperation("FailureModePage", "One", "MaintenanceMasterData.Edit")]
    Task<List<FailureModeFormRow>> OneAsync(int failureModeId);

    [PEMS.Application.Common.PemsOperation("FailureModePage", "ClassOptions", "MaintenanceMasterData.View")]
    Task<List<FailureModeChoice>> ClassOptionsAsync();

    [PEMS.Application.Common.PemsOperation("FailureModePage", "CauseOptions", "MaintenanceMasterData.View")]
    Task<List<FailureModeChoice>> CauseOptionsAsync();
}
