using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// The relationship explorer and the gap report as operations (SCRUM-745). Explore picks the start point by <c>kind</c> (equipment, document, vendor, package, contract; anything else is a free-text search by <c>q</c>);
/// the data sources reach the lists of the answer with their <c>path</c>. Both need Relationship.View, the permission RelationshipController checked.
/// </summary>
public interface IRelationshipPagesOperations
{
    [PEMS.Application.Common.PemsOperation("RelationshipPage", "Explore", "Relationship.View")]
    Task<RelationshipExplorationDto?> ExploreAsync(string? kind, int? id, string? q);

    [PEMS.Application.Common.PemsOperation("RelationshipPage", "GapReport", "Relationship.View")]
    Task<RelationshipGapReportDto> GapReportAsync(int? projectId);
}
