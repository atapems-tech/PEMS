using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class TagService : ITagService
{
    private readonly PEMSDbContext _context;

    public TagService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<TagDto>> GetAllAsync()
    {
        return await _context.Tags
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new TagDto
            {
                TagId = x.TagId,
                Name = x.Name,
                Color = x.Color,
                LetterCount = _context.LetterTags.Count(lt => lt.TagId == x.TagId)
            })
            .ToListAsync();
    }

    public async Task<int> CreateAsync(string name, string color, int? createdById)
    {
        var existing = await _context.Tags.FirstOrDefaultAsync(x => x.Name == name && x.IsActive);
        if (existing != null) return existing.TagId;

        var tag = new Tag
        {
            Name = name,
            Color = string.IsNullOrWhiteSpace(color) ? "secondary" : color,
            CreatedById = createdById,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.Tags.AddAsync(tag);
        await _context.SaveChangesAsync();

        return tag.TagId;
    }

    public async Task DeleteAsync(int tagId)
    {
        var tag = await _context.Tags.FirstOrDefaultAsync(x => x.TagId == tagId);
        if (tag == null) return;

        tag.IsActive = false;
        await _context.SaveChangesAsync();
    }

    public async Task AttachToLetterAsync(int letterId, int tagId)
    {
        var existing = await _context.LetterTags
            .FirstOrDefaultAsync(x => x.LetterId == letterId && x.TagId == tagId);

        if (existing != null) return;

        await _context.LetterTags.AddAsync(new LetterTag
        {
            LetterId = letterId,
            TagId = tagId,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task DetachFromLetterAsync(int letterId, int tagId)
    {
        var existing = await _context.LetterTags
            .FirstOrDefaultAsync(x => x.LetterId == letterId && x.TagId == tagId);

        if (existing == null) return;

        _context.LetterTags.Remove(existing);
        await _context.SaveChangesAsync();
    }

    public async Task<List<TagDto>> GetForLetterAsync(int letterId)
    {
        return await _context.LetterTags
            .Include(x => x.Tag)
            .Where(x => x.LetterId == letterId && x.Tag != null && x.Tag.IsActive)
            .Select(x => new TagDto
            {
                TagId = x.Tag!.TagId,
                Name = x.Tag.Name,
                Color = x.Tag.Color
            })
            .ToListAsync();
    }
}
