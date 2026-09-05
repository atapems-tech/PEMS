using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PEMS.Application.Interfaces;

namespace PEMS.Infrastructure.Cache;

/// <summary>
/// پیاده‌سازی پیش‌فرض ICacheService با IMemoryCache (داخل همان Process، بدون زیرساخت بیرونی).
/// برای استقرار تک‌سروری کاملاً کافی است؛ برای استقرار چندسروری، RedisCacheService جایگزین شود.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached != null)
            return cached;

        var value = await factory();

        _cache.Set(key, value, expiration ?? DefaultExpiration);

        return value;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
