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

    public async Task<RelationshipBulkResult> BulkConfirmAsync(List<string> selectedItems)
    {
        if (selectedItems.Count == 0) return new(false, "No item was selected.");
        // every item is "equipmentId|targetType|targetId" (the format of the checkboxes of the bulk page)
        var parsed = new List<(int EquipmentId, string TargetType, int TargetId)>();
        foreach (var raw in selectedItems)
        {
            var parts = raw.Split('|');
            if (parts.Length == 3 && int.TryParse(parts[0], out var equipmentId) && int.TryParse(parts[2], out var targetId)) parsed.Add((equipmentId, parts[1], targetId));
        }
        var (confirmed, errors) = await service.BulkConfirmAsync(parsed);
        if (errors.Count > 0) return new(false, $"{confirmed} confirmed; {errors.Count} failed: " + string.Join(" | ", errors.Take(5)));
        return new(true, $"{confirmed} suggestions were confirmed and linked.");
    }
}
