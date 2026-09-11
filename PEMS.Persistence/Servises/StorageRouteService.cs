using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۵: مدیریت ادمین روی StorageRoute -
/// قوانین مسیریابی «کدام ماژول/نوع سند روی کدام Provider ذخیره شود». چون هر Module+DocumentType
/// فعال باید یکتا باشد (بررسی duplicateExists زیر)، برای هر Module حداکثر همین حالا هم فقط یک
/// Route با DocumentType=null می‌تواند وجود داشته باشد - پس نیازی به منطق جداگانه‌ی «پاک‌سازی
/// Default قبلی» نیست؛ همان یکتایی Module+DocumentType این تضمین را به‌طور طبیعی می‌دهد.
/// </summary>
public class StorageRouteService : IStorageRouteService
{
    private readonly PEMSDbContext _context;

    public StorageRouteService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<StorageRouteDto>> GetAllAsync()
    {
        return await _context.StorageRoutes
            .Include(x => x.StorageProvider)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Module).ThenBy(x => x.DocumentType)
            .Select(x => new StorageRouteDto
            {
                StorageRouteId = x.StorageRouteId,
                Module = x.Module,
                DocumentType = x.DocumentType,
                StorageProviderId = x.StorageProviderId,
                StorageProviderName = x.StorageProvider != null ? x.StorageProvider.Name : null,
                RelativeRootPath = x.RelativeRootPath,
                IsEnabled = x.IsEnabled,
                IsDefault = x.IsDefault,
                Priority = x.Priority
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(StorageRouteUpsertRequest request, string? createdBy)
    {
        var validationError = await ValidateRequestAsync(request, excludeId: null);
        if (validationError != null) return (false, validationError, null);

        var route = new StorageRoute
        {
            Module = request.Module,
            DocumentType = string.IsNullOrWhiteSpace(request.DocumentType) ? null : request.DocumentType,
            StorageProviderId = request.StorageProviderId,
            RelativeRootPath = request.RelativeRootPath,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            CreatedBy = createdBy,
            CreatedDate = DateTime.Now
        };

        route.IsDefault = request.IsDefault;

        _context.StorageRoutes.Add(route);
        await _context.SaveChangesAsync();

        return (true, null, route.StorageRouteId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, StorageRouteUpsertRequest request, string? modifiedBy)
    {
        var route = await _context.StorageRoutes.FirstOrDefaultAsync(x => x.StorageRouteId == id && x.IsActive);
        if (route == null) return (false, "قانون مسیریابی یافت نشد.");

        var validationError = await ValidateRequestAsync(request, excludeId: id);
        if (validationError != null) return (false, validationError);

        route.Module = request.Module;
        route.DocumentType = string.IsNullOrWhiteSpace(request.DocumentType) ? null : request.DocumentType;
        route.StorageProviderId = request.StorageProviderId;
        route.RelativeRootPath = request.RelativeRootPath;
        route.IsEnabled = request.IsEnabled;
        route.Priority = request.Priority;
        route.ModifiedBy = modifiedBy;

        route.IsDefault = request.IsDefault;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var route = await _context.StorageRoutes.FirstOrDefaultAsync(x => x.StorageRouteId == id && x.IsActive);
        if (route == null) return (false, "قانون مسیریابی یافت نشد.");

        route.IsActive = false;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<string?> ValidateRequestAsync(StorageRouteUpsertRequest request, int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(request.Module)) return "ماژول الزامی است.";

        var provider = await _context.StorageProviders.FirstOrDefaultAsync(x => x.StorageProviderId == request.StorageProviderId && x.IsActive);
        if (provider == null) return "Storage Provider انتخاب‌شده یافت نشد.";

        var documentType = string.IsNullOrWhiteSpace(request.DocumentType) ? null : request.DocumentType;
        var duplicateExists = await _context.StorageRoutes.AnyAsync(x =>
            x.IsActive && x.Module == request.Module && x.DocumentType == documentType
            && (excludeId == null || x.StorageRouteId != excludeId));
        if (duplicateExists)
            return "یک قانون مسیریابی فعال دیگر از قبل برای همین ماژول/نوع سند وجود دارد.";

        return null;
    }
}
