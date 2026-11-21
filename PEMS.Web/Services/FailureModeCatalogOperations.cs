using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-695: the save and toggle of FailureModeCatalogController over IFailureModeCatalogService (same validation, errors translated as before).</summary>
public sealed class FailureModeCatalogOperations(IFailureModeCatalogService catalog, IStringLocalizer<SharedResource> localizer) : IFailureModeCatalogOperations
{
    public Task<(bool Success, string? Error)> CreateAsync(string code, string name, string? nameFa = null, string? description = null, int? equipmentClassId = null, List<int>? causeIds = null, bool isActive = true)
        => SaveAsync(null, code, name, nameFa, description, equipmentClassId, causeIds, isActive);

    public Task<(bool Success, string? Error)> EditAsync(int failureModeId, string code, string name, string? nameFa = null, string? description = null, int? equipmentClassId = null, List<int>? causeIds = null, bool isActive = true)
        => SaveAsync(failureModeId, code, name, nameFa, description, equipmentClassId, causeIds, isActive);

    public async Task<(bool Success, string? Error)> SetActiveAsync(int failureModeId, bool active)
    {
        var result = await catalog.SetActiveAsync(failureModeId, active);
        return result.Success ? (true, null) : (false, string.Join(" ", localizer.TranslateAll(result.Errors)));
    }

    private async Task<(bool Success, string? Error)> SaveAsync(int? id, string code, string name, string? nameFa, string? description, int? equipmentClassId, List<int>? causeIds, bool isActive)
    {
        var result = await catalog.SaveAsync(new FailureModeEditDto
        {
            FailureModeId = id, Code = code, Name = name, NameFa = nameFa, Description = description, EquipmentClassId = equipmentClassId, CauseIds = causeIds ?? [], IsActive = isActive
        });
        return result.Success ? (true, null) : (false, string.Join(" ", localizer.TranslateAll(result.Errors)));
    }
}
