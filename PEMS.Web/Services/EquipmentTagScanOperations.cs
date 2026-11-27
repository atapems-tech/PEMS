using PEMS.Web.Security;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-841: what EquipmentImportController did for the PDF tag scan (see <see cref="IEquipmentTagScanOperations"/>): the same scan, the same rights (Create to scan, Approve to create), the same placeholder equipment.</summary>
public sealed class EquipmentTagScanOperations(IEquipmentPdfScanService scanner, PEMSDbContext context, ICurrentUserService currentUser, ICacheService cache) : IEquipmentTagScanOperations
{
    private static readonly TimeSpan Keep = TimeSpan.FromMinutes(30);
    private string Key(string token) => $"tagscan:{currentUser.UserId}:{token}";

    public async Task<TagScanResult> ScanAsync(int? projectId, OperationFile? file)
    {
        if (projectId is null) return new(false, "Choose a project.", "");
        if (file is null || file.Bytes.Length == 0) return new(false, "Please select a PDF file.", "");
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(tempPath, file.Bytes);
            var candidates = await scanner.ScanForTagCandidatesAsync(tempPath);
            var token = Guid.NewGuid().ToString("N");
            await cache.GetOrCreateAsync(Key(token), () => Task.FromResult(candidates.Distinct().ToList()), Keep);   // the list the user reviews next
            return new(true, null, token);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    public async Task<List<TagScanOption>> CandidatesAsync(string? token = null, int? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(token) || projectId is null || !token.All(char.IsLetterOrDigit)) return [];
        var candidates = await cache.GetOrCreateAsync(Key(token), () => Task.FromResult(new List<string>()), Keep);
        var existing = (await context.Equipments.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => x.TagNumber.ToUpper()).ToListAsync()).ToHashSet();
        return candidates.Where(t => !existing.Contains(t.ToUpper())).Select(t => new TagScanOption(t, t)).ToList();   // a tag that exists is not offered
    }

    public async Task<TagScanResult> CreateAsync(int? projectId, int? disciplineId, string? tags)
    {
        var chosen = (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (projectId is null || disciplineId is null) return new(false, "Choose a project and a discipline.", "0");
        if (chosen.Count == 0) return new(false, "No tags were selected.", "0");

        var existing = (await context.Equipments.Where(x => x.ProjectId == projectId).Select(x => x.TagNumber.ToUpper()).ToListAsync()).ToHashSet();
        var created = 0;
        foreach (var tag in chosen)
        {
            if (existing.Contains(tag.ToUpper())) continue;
            await context.Equipments.AddAsync(new PEMS.Domain.Entities.Equipment
            {
                TagNumber = tag, Name = tag, EquipmentType = "Unspecified", ProjectId = projectId.Value, DisciplineId = disciplineId.Value, Status = "Active", IsActive = true, CreatedDate = DateTime.Now
            });
            created++;
        }
        await context.SaveChangesAsync();
        return new(true, null, created.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<List<TagScanOption>> ProjectsAsync()
        => (await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync()).Select(x => new TagScanOption(x.ProjectId.ToString(CultureInfo.InvariantCulture), x.ProjectName)).ToList();

    public async Task<List<TagScanOption>> DisciplinesAsync()
        => (await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.DisciplineId, x.Name }).ToListAsync()).Select(x => new TagScanOption(x.DisciplineId.ToString(CultureInfo.InvariantCulture), x.Name)).ToList();
}
