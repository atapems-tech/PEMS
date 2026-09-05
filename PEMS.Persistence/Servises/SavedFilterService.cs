using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class SavedFilterService : ISavedFilterService
{
    private readonly PEMSDbContext _context;

    public SavedFilterService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedFilterDto>> GetForPageAsync(int applicationUserId, string pageKey)
    {
        return await _context.SavedFilters
            .Where(x => x.ApplicationUserId == applicationUserId && x.PageKey == pageKey)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name)
            .Select(x => new SavedFilterDto
            {
                SavedFilterId = x.SavedFilterId,
                Name = x.Name,
                QueryString = x.QueryString,
                IsDefault = x.IsDefault
            })
            .ToListAsync();
    }

    public async Task<int> SaveAsync(int applicationUserId, string pageKey, string name, string queryString, bool isDefault)
    {
        if (isDefault)
        {
            var existingDefaults = await _context.SavedFilters
                .Where(x => x.ApplicationUserId == applicationUserId && x.PageKey == pageKey && x.IsDefault)
                .ToListAsync();

            foreach (var d in existingDefaults) d.IsDefault = false;
        }

        var filter = new SavedFilter
        {
            ApplicationUserId = applicationUserId,
            PageKey = pageKey,
            Name = name,
            QueryString = queryString,
            IsDefault = isDefault,
            CreatedDate = DateTime.Now
        };

        await _context.SavedFilters.AddAsync(filter);
        await _context.SaveChangesAsync();

        return filter.SavedFilterId;
    }

    public async Task<SavedFilterDto?> GetDefaultAsync(int applicationUserId, string pageKey)
    {
        var filter = await _context.SavedFilters
            .FirstOrDefaultAsync(x => x.ApplicationUserId == applicationUserId && x.PageKey == pageKey && x.IsDefault);

        if (filter == null) return null;

        return new SavedFilterDto
        {
            SavedFilterId = filter.SavedFilterId,
            Name = filter.Name,
            QueryString = filter.QueryString,
            IsDefault = filter.IsDefault
        };
    }

    public async Task DeleteAsync(int savedFilterId, int applicationUserId)
    {
        var filter = await _context.SavedFilters
            .FirstOrDefaultAsync(x => x.SavedFilterId == savedFilterId && x.ApplicationUserId == applicationUserId);

        if (filter == null) return;

        _context.SavedFilters.Remove(filter);
        await _context.SaveChangesAsync();
    }
}
