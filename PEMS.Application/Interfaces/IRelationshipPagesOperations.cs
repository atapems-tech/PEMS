using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// The relationship explorer and the gap report as operations (SCRUM-745). Explore picks the start point by <c>kind</c> (equipment, document, vendor, package, contract; anything else is a free-text search by <c>q</c>);
/// the data sources reach the lists of the answer with their <c>path</c>. Both need Relationship.View, the permission RelationshipController checked.
/// </summary>
public sealed record RelationshipSuggestedRow(string Link, string Label, string? SubLabel, string NodeType);

public sealed record RelationshipSuggestionChoice(string Value, string Label);

public sealed record RelationshipBulkResult(bool Success, string? Error);

public interface IRelationshipPagesOperations
{
    [PEMS.Application.Common.PemsOperation("RelationshipPage", "Explore", "Relationship.View")]
    Task<RelationshipExplorationDto?> ExploreAsync(string? kind, int? id, string? q);

    [PEMS.Application.Common.PemsOperation("RelationshipPage", "GapReport", "Relationship.View")]
    Task<RelationshipGapReportDto> GapReportAsync(int? projectId);

    /// <summary>The unconfirmed suggestions of an equipment explored by id; Link is the "equipmentId|targetType|targetId" key the confirm action takes.</summary>
    [PEMS.Application.Common.PemsOperation("RelationshipPage", "Suggested", "Relationship.View")]
    Task<List<RelationshipSuggestedRow>> SuggestedAsync(string? kind, int? id, string? q);

    /// <summary>All the unconfirmed suggestions (optionally of a project) as choices of the bulk form: the value is "equipmentId|targetType|targetId".</summary>
    [PEMS.Application.Common.PemsOperation("RelationshipPage", "SuggestionChoices", "Relationship.View")]
    Task<List<RelationshipSuggestionChoice>> SuggestionChoicesAsync(int? projectId = null);

    /// <summary>SCRUM-824: the checked suggestions of the bulk page, each "equipmentId|targetType|targetId"; needs Relationship.Approve as the controller did.</summary>
    [PEMS.Application.Common.PemsOperation("RelationshipPage", "BulkConfirm", "Relationship.Approve")]
    Task<RelationshipBulkResult> BulkConfirmAsync(List<string> selectedItems);
}
