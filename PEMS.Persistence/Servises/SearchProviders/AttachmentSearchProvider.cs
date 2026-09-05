using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Shared.Search;

namespace PEMS.Persistence.Services.SearchProviders;

/// <summary>
/// جستجوی پیوست‌ها (Attachments) - بر اساس نام فایل و متادیتای استخراج‌شده توسط AI
/// (AISummary/AIKeywords/AITags) هنگام Intake.
/// </summary>
public class AttachmentSearchProvider : ISearchableEntityProvider
{
    private readonly PEMSDbContext _context;

    public string EntityType => "Attachment";
    public string DisplayName => "پیوست‌ها";

    public AttachmentSearchProvider(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<(List<GlobalSearchResultItemDto> Items, int TotalCount)> SearchAsync(
        string query, int page, int pageSize, int? projectId)
    {
        var candidatesQuery = _context.DocumentAttachments
            .Where(x => x.IsActive)
            .Where(x =>
                x.FileName.Contains(query) ||
                (x.AISummary != null && x.AISummary.Contains(query)) ||
                (x.AIKeywords != null && x.AIKeywords.Contains(query)) ||
                (x.AITags != null && x.AITags.Contains(query)));

        if (projectId.HasValue)
            candidatesQuery = candidatesQuery.Where(x => x.IntakeProjectId == projectId.Value);

        var pool = await candidatesQuery.Take(500).ToListAsync();

        var scored = pool
            .Select(a => new
            {
                Attachment = a,
                Score = SearchScoring.ComputeWeightedScore(query,
                    (a.FileName, 1.0), (a.AIKeywords, 0.6), (a.AITags, 0.6), (a.AISummary, 0.4))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenByDescending(x => x.Attachment.UploadDate)
            .ToList();

        var totalCount = scored.Count;
        var pageItems = scored.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new GlobalSearchResultItemDto
            {
                EntityType = EntityType,
                Id = x.Attachment.AttachmentId,
                Label = x.Attachment.FileName,
                HighlightedLabel = SearchScoring.Highlight(x.Attachment.FileName, query),
                SubLabel = x.Attachment.AISummary != null && x.Attachment.AISummary.Length > 100
                    ? x.Attachment.AISummary[..100] + "..."
                    : x.Attachment.AISummary,
                // مقصد بسته به این‌که پیوست به کدام مدرک/نامه وصل شده متفاوت است
                Url = x.Attachment.DocumentId.HasValue ? $"/EngineeringDocuments/Details/{x.Attachment.DocumentId}"
                    : x.Attachment.LetterId.HasValue ? $"/Letter/Details/{x.Attachment.LetterId}"
                    : null,
                Score = x.Score
            }).ToList();

        return (pageItems, totalCount);
    }
}
