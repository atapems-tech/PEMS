namespace PEMS.Application.Interfaces;

/// <summary>
/// لایه‌ی Caching طبق Volume II-Bible ("Caching: Redis"). پیاده‌سازی پیش‌فرض In-Memory است
/// (بدون نیاز به زیرساخت بیرونی)؛ اگر Connection String واقعی Redis در تنظیمات باشد، خودکار
/// به Redis سوییچ می‌کند - بدون تغییر در هیچ کدی که از این Interface استفاده می‌کند.
/// </summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    Task RemoveAsync(string key);
}
