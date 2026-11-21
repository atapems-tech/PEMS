namespace PEMS.Application.Interfaces;

/// <summary>The write commands of FailureModeCatalogController as operations (SCRUM-695): save a failure mode of the ISO 14224 catalogue and switch it active or inactive. Same service, same permissions (MaintenanceMasterData Create/Edit).</summary>
public interface IFailureModeCatalogOperations
{
    [PEMS.Application.Common.PemsOperation("FailureMode", "Create", "MaintenanceMasterData.Create")]
    Task<(bool Success, string? Error)> CreateAsync(string code, string name, string? nameFa = null, string? description = null, int? equipmentClassId = null, List<int>? causeIds = null, bool isActive = true);

    [PEMS.Application.Common.PemsOperation("FailureMode", "Edit", "MaintenanceMasterData.Edit")]
    Task<(bool Success, string? Error)> EditAsync(int failureModeId, string code, string name, string? nameFa = null, string? description = null, int? equipmentClassId = null, List<int>? causeIds = null, bool isActive = true);

    [PEMS.Application.Common.PemsOperation("FailureMode", "SetActive", "MaintenanceMasterData.Edit")]
    Task<(bool Success, string? Error)> SetActiveAsync(int failureModeId, bool active);
}
