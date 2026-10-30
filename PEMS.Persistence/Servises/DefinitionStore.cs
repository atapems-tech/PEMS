using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PEMS.Application.Definitions;
using PEMS.Domain.Entities.Definitions;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Runtime read side of the definition store (ADR-0010 section 5): the published version only, cached per (kind, key). A cached entry is trusted while
/// the per-kind stamp is unchanged; the stamp itself is read at most once per second, so another instance sees a publish within about 2 seconds,
/// and the instance that published evicts at once.
/// </summary>
public sealed class DefinitionStore : IDefinitionStore
{
    private static readonly TimeSpan StampLifetime = TimeSpan.FromSeconds(1);
    private readonly PEMSDbContext _db;
    private readonly IMemoryCache _cache;

    public DefinitionStore(PEMSDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private sealed record Entry(long Stamp, PublishedDefinition? Value);

    public async Task<PublishedDefinition?> GetPublishedAsync(string kind, string key, string appKey = "core", CancellationToken ct = default)
    {
        var stamp = await StampAsync(kind, ct);
        var cacheKey = $"def:{appKey}:{kind}:{key}";
        if (_cache.TryGetValue(cacheKey, out Entry? hit) && hit!.Stamp == stamp) return hit.Value;

        var row = await _db.Definitions.AsNoTracking()
            .Where(d => d.AppKey == appKey && d.Kind == kind && d.Key == key && d.PublishedVersionId != null)
            .Select(d => new PublishedDefinition(d.Kind, d.Key, d.AppKey, d.PublishedVersion!.VersionNumber, d.PublishedVersion.PayloadJson, d.PublishedVersion.ContentHash))
            .FirstOrDefaultAsync(ct);
        _cache.Set(cacheKey, new Entry(stamp, row), TimeSpan.FromHours(1));
        return row;
    }

    private async Task<long> StampAsync(string kind, CancellationToken ct)
    {
        var stampKey = "defstamp:" + kind;
        if (_cache.TryGetValue(stampKey, out long cached)) return cached;
        var value = await _db.DefinitionStamps.AsNoTracking().Where(s => s.Kind == kind).Select(s => s.Value).FirstOrDefaultAsync(ct);
        _cache.Set(stampKey, value, StampLifetime);
        return value;
    }

    /// <summary>Called by the writer after a publish: the next read sees the new stamp immediately.</summary>
    public void Evict(string kind) => _cache.Remove("defstamp:" + kind);
}
