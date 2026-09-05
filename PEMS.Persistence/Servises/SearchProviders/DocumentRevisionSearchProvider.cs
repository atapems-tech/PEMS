using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Shared.Search;

namespace PEMS.Persistence.Services.SearchProviders;

/// <summary>
/// جستجوی رویژن‌های مدارک - عمدتاً بر اساس شماره‌ی مدرک والد + شماره‌ی رویژن (چون کاربر
/// معمولاً دنبال «رویژن X از مدرک Y» می‌گردد، نه یک رویژن مستقل بی‌ارتباط).
/// </summary>
public class DocumentRevisionSearchProvider : ISearchableEntityProvider
{
    private readonly PEMSDbContext _context;

    public string EntityType => "DocumentRevision";
    public string DisplayName => "رویژن مدارک";

    public DocumentRevisionSearchProvider(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<(List<GlobalSearchResultItemDto> Items, int TotalCount)> SearchAsync(
        string query, int page, int pageSize, int? projectId)
    {
        var candidatesQuery = _context.DocumentRevisions
            .Include(x => x.Document)
            .Where(x => x.IsActive)
            .Where(x =>
                (x.Document != null && x.Document.DocumentNo.Contains(query)) ||
                x.RevisionNo.Contains(query) ||
                (x.Comment != null && x.Comment.Contains(query)));

        if (projectId.HasValue)
            candidatesQuery = candidatesQuery.Where(x => x.Document != null && x.Document.ProjectId == projectId.Value);

        var pool = await candidatesQuery.OrderByDescending(x => x.RevisionDate).Take(300).ToListAsync();

        var scored = pool
            .Where(r => r.Document != null)
            .Select(r => new
            {
                Revision = r,
                CombinedLabel = $"{r.Document!.DocumentNo} Rev.{r.RevisionNo}",
                Score = SearchScoring.ComputeWeightedScore(query,
                    (r.Document!.DocumentNo, 1.0), (r.RevisionNo, 0.8), (r.Comment, 0.4))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenByDescending(x => x.Revision.RevisionDate)
            .ToList();

        var totalCount = scored.Count;
        var pageItems = scored.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new GlobalSearchResultItemDto
            {
                EntityType = EntityType,
                Id = x.Revision.RevisionId,
                Label = x.CombinedLabel,
                HighlightedLabel = SearchScoring.Highlight(x.CombinedLabel, query),
                SubLabel = string.Join(" — ", new[] { x.Revision.Status, x.Revision.Comment }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Url = $"/EngineeringDocuments/Details/{x.Revision.DocumentId}",
                Score = x.Score
            }).ToList();

        return (pageItems, totalCount);
    }
}
