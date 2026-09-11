using Microsoft.EntityFrameworkCore;
using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۸: ترتیب صریح تفکیک -
/// ۱) Route فعال دقیق Module+DocumentType (بالاترین Priority در صورت چند مورد)
/// ۲) Route فعال Module-Default (DocumentType=null, IsDefault=true) برای همان Module
/// ۳) StorageProvider سراسری پیش‌فرض (IsDefault=true, IsEnabled=true, IsActive=true)
/// هرگز حدس زده نمی‌شود - اگر هیچ‌کدام یافت نشود، StorageConfigurationInvalid پرتاب می‌شود.
/// </summary>
public class StorageRouteResolver : IStorageRouteResolver
{
    private readonly PEMSDbContext _context;

    public StorageRouteResolver(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<StorageProvider> ResolveAsync(string module, string? documentType = null, CancellationToken cancellationToken = default)
    {
        StorageRoute? route = null;

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            route = await _context.StorageRoutes
                .Include(x => x.StorageProvider)
                .Where(x => x.IsActive && x.IsEnabled && x.Module == module && x.DocumentType == documentType)
                .OrderByDescending(x => x.Priority)
                .FirstOrDefaultAsync(cancellationToken);
        }

        route ??= await _context.StorageRoutes
            .Include(x => x.StorageProvider)
            .Where(x => x.IsActive && x.IsEnabled && x.Module == module && x.DocumentType == null && x.IsDefault)
            .OrderByDescending(x => x.Priority)
            .FirstOrDefaultAsync(cancellationToken);

        if (route?.StorageProvider is { IsActive: true, IsEnabled: true })
            return route.StorageProvider;

        var globalDefault = await _context.StorageProviders
            .Where(x => x.IsActive && x.IsEnabled && x.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        if (globalDefault != null) return globalDefault;

        throw new StorageException(StorageErrorCodes.StorageConfigurationInvalid,
            $"هیچ StorageProvider ای برای ماژول «{module}» پیکربندی نشده و هیچ Provider سراسری پیش‌فرضی هم فعال نیست.");
    }
}
