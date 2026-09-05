using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Shared.Search;

namespace PEMS.Persistence.Services.SearchProviders;

public class MaterialRequisitionSearchProvider : ISearchableEntityProvider
{
    private readonly PEMSDbContext _context;

    public string EntityType => "MaterialRequisition";
    public string DisplayName => "درخواست‌های خرید (MR/MRQ)";

    public MaterialRequisitionSearchProvider(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<(List<GlobalSearchResultItemDto> Items, int TotalCount)> SearchAsync(
        string query, int page, int pageSize, int? projectId)
    {
        var candidatesQuery = _context.MaterialRequisitions
            .Include(x => x.Discipline)
            .Where(x => x.MRNumber.Contains(query) || x.Description.Contains(query)
                || (x.EquipmentTagNumber != null && x.EquipmentTagNumber.Contains(query)));

        if (projectId.HasValue) candidatesQuery = candidatesQuery.Where(x => x.ProjectId == projectId.Value);

        var pool = await candidatesQuery.Take(500).ToListAsync();

        if (pool.Count < 5 && query.Length is >= 2 and <= 25)
        {
            var fallbackQuery = _context.MaterialRequisitions.Include(x => x.Discipline).AsQueryable();
            if (projectId.HasValue) fallbackQuery = fallbackQuery.Where(x => x.ProjectId == projectId.Value);
            var fallback = await fallbackQuery.Take(500).ToListAsync();
            foreach (var m in fallback)
                if (pool.All(x => x.MRId != m.MRId)) pool.Add(m);
        }

        var scored = pool
            .Select(m => new
            {
                MR = m,
                Score = SearchScoring.ComputeWeightedScore(query,
                    (m.MRNumber, 1.0), (m.Description, 0.6), (m.EquipmentTagNumber, 0.7))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.MR.MRNumber)
            .ToList();

        var totalCount = scored.Count;
        var pageItems = scored.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new GlobalSearchResultItemDto
            {
                EntityType = EntityType,
                Id = x.MR.MRId,
                Label = x.MR.MRNumber,
                HighlightedLabel = SearchScoring.Highlight(x.MR.MRNumber, query),
                SubLabel = string.Join(" — ", new[] { x.MR.Description, x.MR.Discipline?.Name, x.MR.Status }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Url = $"/MaterialRequisition/Details/{x.MR.MRId}",
                Score = x.Score
            }).ToList();

        return (pageItems, totalCount);
    }
}
