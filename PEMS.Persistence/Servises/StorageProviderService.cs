using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۲/۱۳: مدیریت ادمین کامل روی
/// StorageProvider - رمز عبور فقط در Create/Update (وقتی مقدار دارد) رمزنگاری می‌شود و هرگز
/// در DTO خروجی بازنمی‌گردد. حذف وقتی Route فعال به این Provider ارجاع دارد مسدود می‌شود.
/// </summary>
public class StorageProviderService : IStorageProviderService
{
    private readonly PEMSDbContext _context;
    private readonly ICredentialProtector _credentialProtector;
    private readonly IStorageProviderClientFactory _clientFactory;

    public StorageProviderService(PEMSDbContext context, ICredentialProtector credentialProtector, IStorageProviderClientFactory clientFactory)
    {
        _context = context;
        _credentialProtector = credentialProtector;
        _clientFactory = clientFactory;
    }

    public async Task<List<StorageProviderDto>> GetAllAsync()
    {
        var providers = await _context.StorageProviders
            .Include(x => x.Routes)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return providers.Select(x => MapToDto(x, x.Routes.Count(r => r.IsActive))).ToList();
    }

    public async Task<StorageProviderDto?> GetByIdAsync(int id)
    {
        var provider = await _context.StorageProviders
            .Include(x => x.Routes)
            .FirstOrDefaultAsync(x => x.StorageProviderId == id && x.IsActive);

        return provider == null ? null : MapToDto(provider, provider.Routes.Count(r => r.IsActive));
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(StorageProviderUpsertRequest request, string? createdBy)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null) return (false, validationError, null);

        var codeExists = await _context.StorageProviders.AnyAsync(x => x.IsActive && x.Code == request.Code);
        if (codeExists) return (false, $"کد «{request.Code}» قبلاً برای یک Storage Provider دیگر استفاده شده است.", null);

        var provider = new StorageProvider
        {
            Name = request.Name,
            Code = request.Code,
            ProviderType = request.ProviderType,
            Host = request.Host,
            Port = request.Port,
            Username = request.Username,
            EncryptedPassword = string.IsNullOrEmpty(request.Password) ? null : _credentialProtector.Protect(request.Password),
            RootPath = request.RootPath,
            BaseUrl = request.BaseUrl,
            UseSsl = request.UseSsl,
            UsePassiveMode = request.UsePassiveMode,
            AllowRead = request.AllowRead,
            AllowWrite = request.AllowWrite,
            AllowDelete = request.AllowDelete,
            IsEnabled = request.IsEnabled,
            IsDefault = false,
            LastHealthStatus = StorageHealthStatuses.Unknown,
            CreatedBy = createdBy,
            CreatedDate = DateTime.Now
        };

        _context.StorageProviders.Add(provider);
        await _context.SaveChangesAsync();

        return (true, null, provider.StorageProviderId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, StorageProviderUpsertRequest request, string? modifiedBy)
    {
        var provider = await _context.StorageProviders.FirstOrDefaultAsync(x => x.StorageProviderId == id && x.IsActive);
        if (provider == null) return (false, "Storage Provider یافت نشد.");

        var validationError = ValidateRequest(request);
        if (validationError != null) return (false, validationError);

        var codeExists = await _context.StorageProviders.AnyAsync(x => x.IsActive && x.Code == request.Code && x.StorageProviderId != id);
        if (codeExists) return (false, $"کد «{request.Code}» قبلاً برای یک Storage Provider دیگر استفاده شده است.");

        provider.Name = request.Name;
        provider.Code = request.Code;
        provider.ProviderType = request.ProviderType;
        provider.Host = request.Host;
        provider.Port = request.Port;
        provider.Username = request.Username;
        // بند ۱۲: خالی گذاشتن رمز عبور یعنی رمز فعلی دست‌نخورده بماند
        if (!string.IsNullOrEmpty(request.Password))
            provider.EncryptedPassword = _credentialProtector.Protect(request.Password);
        provider.RootPath = request.RootPath;
        provider.BaseUrl = request.BaseUrl;
        provider.UseSsl = request.UseSsl;
        provider.UsePassiveMode = request.UsePassiveMode;
        provider.AllowRead = request.AllowRead;
        provider.AllowWrite = request.AllowWrite;
        provider.AllowDelete = request.AllowDelete;
        provider.IsEnabled = request.IsEnabled;
        provider.ModifiedBy = modifiedBy;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetEnabledAsync(int id, bool enabled, string? modifiedBy)
    {
        var provider = await _context.StorageProviders.FirstOrDefaultAsync(x => x.StorageProviderId == id && x.IsActive);
        if (provider == null) return (false, "Storage Provider یافت نشد.");

        if (!enabled && provider.IsDefault)
            return (false, "امکان غیرفعال‌سازی Storage Provider پیش‌فرض سیستم وجود ندارد. ابتدا یک Provider دیگر را پیش‌فرض کنید.");

        provider.IsEnabled = enabled;
        provider.ModifiedBy = modifiedBy;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetDefaultAsync(int id, string? modifiedBy)
    {
        var provider = await _context.StorageProviders.FirstOrDefaultAsync(x => x.StorageProviderId == id && x.IsActive);
        if (provider == null) return (false, "Storage Provider یافت نشد.");
        if (!provider.IsEnabled) return (false, "امکان تعیین یک Storage Provider غیرفعال به‌عنوان پیش‌فرض وجود ندارد.");

        var currentDefaults = await _context.StorageProviders
            .Where(x => x.IsActive && x.IsDefault && x.StorageProviderId != id)
            .ToListAsync();
        foreach (var other in currentDefaults) other.IsDefault = false;

        provider.IsDefault = true;
        provider.ModifiedBy = modifiedBy;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var provider = await _context.StorageProviders.FirstOrDefaultAsync(x => x.StorageProviderId == id && x.IsActive);
        if (provider == null) return (false, "Storage Provider یافت نشد.");

        var hasActiveRoutes = await _context.StorageRoutes.AnyAsync(x => x.StorageProviderId == id && x.IsActive);
        if (hasActiveRoutes)
            return (false, "این Storage Provider توسط یک یا چند قانون مسیریابی فعال استفاده می‌شود و قابل حذف نیست.");

        if (provider.IsDefault)
            return (false, "امکان حذف Storage Provider پیش‌فرض سیستم وجود ندارد. ابتدا یک Provider دیگر را پیش‌فرض کنید.");

        provider.IsActive = false;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<StorageHealthResultDto> TestConnectionAsync(int id)
    {
        var provider = await _context.StorageProviders.FirstOrDefaultAsync(x => x.StorageProviderId == id && x.IsActive);
        if (provider == null)
        {
            return new StorageHealthResultDto
            {
                OverallStatus = StorageHealthStatuses.Unknown,
                Message = "Storage Provider یافت نشد."
            };
        }

        var client = _clientFactory.Create(provider);
        var result = await client.TestConnectionAsync();

        provider.LastHealthCheckAt = DateTime.Now;
        provider.LastHealthStatus = result.OverallStatus;
        provider.LastHealthMessage = result.Message;
        await _context.SaveChangesAsync();

        return result;
    }

    private static string? ValidateRequest(StorageProviderUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "نام Storage Provider الزامی است.";
        if (string.IsNullOrWhiteSpace(request.Code)) return "کد Storage Provider الزامی است.";
        if (!Domain.Entities.StorageProviderTypes.All.Contains(request.ProviderType))
            return $"نوع Provider «{request.ProviderType}» معتبر نیست.";
        if (string.IsNullOrWhiteSpace(request.RootPath)) return "مسیر ریشه (Root Path) الزامی است.";

        if (Domain.Entities.StorageProviderTypes.RequiresNetworkCredentials(request.ProviderType))
        {
            if (string.IsNullOrWhiteSpace(request.Host)) return "برای FTP/SFTP، آدرس سرور (Host) الزامی است.";
            if (string.IsNullOrWhiteSpace(request.Username)) return "برای FTP/SFTP، نام کاربری الزامی است.";
        }

        return null;
    }

    private static StorageProviderDto MapToDto(StorageProvider x, int activeRouteCount) => new()
    {
        StorageProviderId = x.StorageProviderId,
        Name = x.Name,
        Code = x.Code,
        ProviderType = x.ProviderType,
        Host = x.Host,
        Port = x.Port,
        Username = x.Username,
        HasPassword = !string.IsNullOrEmpty(x.EncryptedPassword),
        RootPath = x.RootPath,
        BaseUrl = x.BaseUrl,
        UseSsl = x.UseSsl,
        UsePassiveMode = x.UsePassiveMode,
        AllowRead = x.AllowRead,
        AllowWrite = x.AllowWrite,
        AllowDelete = x.AllowDelete,
        IsEnabled = x.IsEnabled,
        IsDefault = x.IsDefault,
        LastHealthCheckAt = x.LastHealthCheckAt,
        LastHealthStatus = x.LastHealthStatus,
        LastHealthMessage = x.LastHealthMessage,
        ActiveRouteCount = activeRouteCount
    };
}
