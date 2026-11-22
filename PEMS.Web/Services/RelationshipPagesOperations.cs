using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-745: the explorer and the gap report over the same service the controller called (see <see cref="IRelationshipPagesOperations"/>).</summary>
public sealed class RelationshipPagesOperations(IRelationshipService service) : IRelationshipPagesOperations
{
    public Task<RelationshipExplorationDto?> ExploreAsync(string? kind, int? id, string? q)
    {
        switch ((kind ?? "").ToLowerInvariant())
        {
            case "equipment" when id is not null: return service.ExploreFromEquipmentAsync(id.Value);
            case "document" when id is not null: return service.ExploreFromDocumentAsync(id.Value);
            case "vendor" when id is not null: return service.ExploreFromVendorAsync(id.Value);
            case "package" when id is not null: return service.ExploreFromProcurementPackageAsync(id.Value);
            case "contract" when id is not null: return service.ExploreFromContractAsync(id.Value);
        }
        return string.IsNullOrWhiteSpace(q) ? Task.FromResult<RelationshipExplorationDto?>(null) : service.ExploreByFreeTextAsync(q);
    }

    public Task<RelationshipGapReportDto> GapReportAsync(int? projectId) => service.GetGapReportAsync(projectId);
}
