using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class DisciplineMasterService : IDisciplineMasterService
{
    private readonly PEMSDbContext _ctx;

    public DisciplineMasterService(PEMSDbContext ctx) => _ctx = ctx;

    public async Task<List<DisciplineRow>> GetAllAsync(CancellationToken ct = default)
    {
        var empCounts = await _ctx.Employees.AsNoTracking().GroupBy(e => e.DisciplineId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var docCounts = await _ctx.EngineeringDocuments.AsNoTracking().Where(d => d.DisciplineId != null).GroupBy(d => d.DisciplineId!.Value).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var rows = await _ctx.Disciplines.AsNoTracking().OrderByDescending(d => d.IsActive).ThenBy(d => d.Name).ThenBy(d => d.Code).ToListAsync(ct);
        return rows.Select(d => new DisciplineRow(d.DisciplineId, d.Code, d.Name, d.Description, d.ImportAliases, d.TransmittalCode, d.IsActive,
            empCounts.GetValueOrDefault(d.DisciplineId), docCounts.GetValueOrDefault(d.DisciplineId))).ToList();
    }

    public async Task<DisciplineOpResult> CreateAsync(string code, string name, string? description, string? importAliases, string? transmittalCode, CancellationToken ct = default)
    {
        var (err, c, n, aliases, tc) = await NormalizeAsync(null, code, name, importAliases, transmittalCode, ct);
        if (err != null) return DisciplineOpResult.Fail(err);
        _ctx.Disciplines.Add(new Discipline { Code = c, Name = n, Description = Clean(description), ImportAliases = aliases, TransmittalCode = tc, IsActive = true, CreatedDate = DateTime.Now });
        await _ctx.SaveChangesAsync(ct);
        return DisciplineOpResult.Ok();
    }

    public async Task<DisciplineOpResult> UpdateAsync(int disciplineId, string code, string name, string? description, string? importAliases, string? transmittalCode, CancellationToken ct = default)
    {
        var d = await _ctx.Disciplines.FirstOrDefaultAsync(x => x.DisciplineId == disciplineId, ct);
        if (d == null) return DisciplineOpResult.Fail("Disc_Err_NotFound");
        var (err, c, n, aliases, tc) = await NormalizeAsync(disciplineId, code, name, importAliases, transmittalCode, ct);
        if (err != null) return DisciplineOpResult.Fail(err);
        d.Code = c; d.Name = n; d.Description = Clean(description); d.ImportAliases = aliases; d.TransmittalCode = tc;
        await _ctx.SaveChangesAsync(ct);
        return DisciplineOpResult.Ok();
    }

    public async Task<DisciplineOpResult> SetActiveAsync(int disciplineId, bool isActive, CancellationToken ct = default)
    {
        var d = await _ctx.Disciplines.FirstOrDefaultAsync(x => x.DisciplineId == disciplineId, ct);
        if (d == null) return DisciplineOpResult.Fail("Disc_Err_NotFound");
        d.IsActive = isActive;
        await _ctx.SaveChangesAsync(ct);
        return DisciplineOpResult.Ok();
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private async Task<(string? Error, string Code, string Name, string? Aliases, string? TransmittalCode)> NormalizeAsync(
        int? selfId, string code, string name, string? importAliases, string? transmittalCode, CancellationToken ct)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        var n = (name ?? "").Trim();
        if (c.Length == 0 || n.Length == 0) return ("Disc_Err_Required", c, n, null, null);
        if (c.Length > 20 || n.Length > 150) return ("Disc_Err_TooLong", c, n, null, null);

        var tc = Clean(transmittalCode)?.ToUpperInvariant();
        if (tc != null && (tc.Length != 1 || !char.IsLetter(tc[0]))) return ("Disc_Err_TransmittalCode", c, n, null, null);

        var aliases = (importAliases ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var others = await _ctx.Disciplines.AsNoTracking().Where(d => selfId == null || d.DisciplineId != selfId)
            .Select(d => new { d.Code, d.Name, d.ImportAliases }).ToListAsync(ct);
        if (others.Any(o => o.Code.Equals(c, StringComparison.OrdinalIgnoreCase))) return ("Disc_Err_DuplicateCode", c, n, null, null);
        if (others.Any(o => o.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) return ("Disc_Err_DuplicateName", c, n, null, null);

        // Alias یا Code/Name خودش با هر شناسه‌ی دیسیپلین دیگر نباید برابر باشد (وگرنه Import مبهم می‌شود)
        var foreign = others.SelectMany(o => new[] { o.Code, o.Name }.Concat((o.ImportAliases ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (aliases.Any(a => foreign.Contains(a))) return ("Disc_Err_AliasConflict", c, n, null, null);

        return (null, c, n, aliases.Count == 0 ? null : string.Join(", ", aliases), tc);
    }
}
