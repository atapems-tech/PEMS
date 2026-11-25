using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-698: the failure mode catalogue reads over the same service and tables FailureModeCatalogController used (see <see cref="IFailureModeCatalogPagesOperations"/>).</summary>
public sealed class FailureModeCatalogPagesOperations(PEMSDbContext context, IFailureModeCatalogService catalog) : IFailureModeCatalogPagesOperations
{
    public async Task<List<FailureModeCatalogRow>> ListAsync(int? classId = null, string? q = null)
        => (await catalog.SearchAsync(classId, q, 200)).Select(m => new FailureModeCatalogRow(m.FailureModeId, m.Code, m.Name, m.NameFa, m.EquipmentClassName,
            string.Join(", ", m.Causes.Select(c => string.IsNullOrWhiteSpace(c.NameFa) ? c.Name : c.Name + " / " + c.NameFa)))).ToList();

    public async Task<List<FailureModeFormRow>> OneAsync(int failureModeId)
    {
        var dto = await catalog.GetAsync(failureModeId);
        return dto is null ? [] : [new FailureModeFormRow(dto.FailureModeId ?? failureModeId, dto.Code, dto.Name, dto.NameFa, dto.Description, dto.EquipmentClassId, string.Join(",", dto.CauseIds), dto.IsActive)];
    }

    public async Task<List<FailureModeChoice>> ClassOptionsAsync()
        => await context.EquipmentClasses.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => new FailureModeChoice(c.EquipmentClassId.ToString(), c.Name + " (" + c.Code + ")")).ToListAsync();

    public async Task<List<FailureModeChoice>> CauseOptionsAsync()
        => await context.FailureCauses.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => new FailureModeChoice(c.FailureCauseId.ToString(), c.Name)).ToListAsync();
}
