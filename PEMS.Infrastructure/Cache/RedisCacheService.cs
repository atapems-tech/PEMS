using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PEMS.Application.Interfaces;

namespace PEMS.Infrastructure.Cache;

/// <summary>
/// پیاده‌سازی توزیع‌شده‌ی ICacheService با Redis - برای استقرار چندسروری/Cloud (طبق Volume II-Bible).
/// فقط وقتی فعال می‌شود که ConnectionStrings:Redis در تنظیمات مقداردهی شده باشد
/// (Program.cs تصمیم می‌گیرد کدام پیاده‌سازی Register شود).
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        var cachedJson = await _cache.GetStringAsync(key);

        if (!string.IsNullOrEmpty(cachedJson))
        {
            var cached = JsonSerializer.Deserialize<T>(cachedJson);
            if (cached != null) return cached;
        }

        var value = await factory();

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        });

        return value;
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}
