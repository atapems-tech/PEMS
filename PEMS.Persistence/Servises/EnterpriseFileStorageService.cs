using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲/۲۰: نمای کسب‌وکاری چند-Provider -
/// مسیر فیزیکی همیشه قطعی و امن تولید می‌شود، هرگز از نام فایل ورودی کاربر مشتق نمی‌شود.
/// SHA256 حین Streaming (نه با خواندن دوباره‌ی فایل) محاسبه می‌شود.
/// </summary>
public class EnterpriseFileStorageService : IEnterpriseFileStorageService
{
    private readonly IStorageRouteResolver _routeResolver;
    private readonly IStorageProviderClientFactory _clientFactory;
    private readonly PEMS.Persistence.Context.PEMSDbContext _context;

    public EnterpriseFileStorageService(IStorageRouteResolver routeResolver, IStorageProviderClientFactory clientFactory,
        PEMS.Persistence.Context.PEMSDbContext context)
    {
        _routeResolver = routeResolver;
        _clientFactory = clientFactory;
        _context = context;
    }

    public async Task<EnterpriseStorageResultDto> SaveAsync(
        string module, string? documentType, int? entityId,
        string originalFileName, Stream content, string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var provider = await _routeResolver.ResolveAsync(module, documentType, cancellationToken);
        var client = _clientFactory.Create(provider);

        var extension = Path.GetExtension(originalFileName);
        var storedName = $"{Guid.NewGuid()}{extension}";

        // طبق بند ۲۰: مسیر قطعی و امن - {Module}/{Year}/{EntityId?}/{Guid}{Ext}
        var pathParts = new List<string> { module, DateTime.Now.Year.ToString() };
        if (entityId.HasValue) pathParts.Add(entityId.Value.ToString());
        pathParts.Add(storedName);
        var relativePath = string.Join("/", pathParts);

        var hashingStream = new HashingStream(content);
        await client.SaveAsync(relativePath, hashingStream, cancellationToken);

        return new EnterpriseStorageResultDto
        {
            StorageProviderId = provider.StorageProviderId,
            StorageProviderCode = provider.Code,
            RelativePath = relativePath,
            Sha256 = hashingStream.GetHexHash(),
            FileSizeBytes = hashingStream.BytesRead,
            SavedAt = DateTime.Now
        };
    }

    public async Task<Stream> OpenReadAsync(int storageProviderId, string relativePath, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(storageProviderId);
        return await _clientFactory.Create(provider).OpenReadAsync(relativePath, cancellationToken);
    }

    public async Task<bool> ExistsAsync(int storageProviderId, string relativePath, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(storageProviderId);
        return await _clientFactory.Create(provider).ExistsAsync(relativePath, cancellationToken);
    }

    public async Task DeleteAsync(int storageProviderId, string relativePath, CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderOrThrowAsync(storageProviderId);
        await _clientFactory.Create(provider).DeleteAsync(relativePath, cancellationToken);
    }

    private async Task<Domain.Entities.StorageProvider> GetProviderOrThrowAsync(int storageProviderId)
    {
        var provider = await _context.StorageProviders.FindAsync(storageProviderId);
        if (provider == null || !provider.IsActive)
            throw new Application.Exceptions.StorageException(Application.Exceptions.StorageErrorCodes.StorageProviderNotFound, "Storage Provider یافت نشد.");
        if (!provider.IsEnabled)
            throw new Application.Exceptions.StorageException(Application.Exceptions.StorageErrorCodes.StorageProviderDisabled, "این Storage Provider غیرفعال است.");
        return provider;
    }
}
