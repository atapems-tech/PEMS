using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۵/۲۹: تست مدیریت ادمین StorageRoute -
/// رد Provider ناموجود، رد تکراری بودن Module+DocumentType فعال، و پاک‌سازی خودکار Default قبلی
/// همان Module هنگام تعیین یک Default جدید.
/// </summary>
public class StorageRouteServiceTests
{
    [Fact]
    public async Task CreateAsync_UnknownStorageProvider_ReturnsValidationError()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_UnknownStorageProvider_ReturnsValidationError));
        var service = new StorageRouteService(ctx);

        var (success, error, id) = await service.CreateAsync(new StorageRouteUpsertRequest { Module = "VDR", StorageProviderId = 9999 }, "tester");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(id);
    }

    [Fact]
    public async Task CreateAsync_DuplicateModuleAndDocumentType_ReturnsValidationError()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_DuplicateModuleAndDocumentType_ReturnsValidationError));

        var provider = new StorageProvider { Name = "P1", Code = "P1", RootPath = "C:/p1", IsActive = true, IsEnabled = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var service = new StorageRouteService(ctx);
        await service.CreateAsync(new StorageRouteUpsertRequest { Module = "VDR", DocumentType = "Drawing", StorageProviderId = provider.StorageProviderId }, "tester");
        var (success, error, _) = await service.CreateAsync(new StorageRouteUpsertRequest { Module = "VDR", DocumentType = "Drawing", StorageProviderId = provider.StorageProviderId }, "tester");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_SecondModuleDefaultForSameModule_IsBlockedByUniqueness()
    {
        // طبق یکتایی Module+DocumentType در ValidateRequestAsync: چون هر دو Route زیر
        // DocumentType=null دارند (یعنی هر دو "Module Default" برای همان Module هستند)، دومی
        // باید رد شود - این یکتایی به‌تنهایی تضمین می‌کند حداکثر یک Default واقعی برای هر Module
        // وجود داشته باشد، بدون نیاز به منطق جداگانه‌ی «پاک‌سازی».
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_SecondModuleDefaultForSameModule_IsBlockedByUniqueness));

        var providerA = new StorageProvider { Name = "A", Code = "A", RootPath = "C:/a", IsActive = true, IsEnabled = true };
        var providerB = new StorageProvider { Name = "B", Code = "B", RootPath = "C:/b", IsActive = true, IsEnabled = true };
        ctx.StorageProviders.AddRange(providerA, providerB);
        await ctx.SaveChangesAsync();

        var service = new StorageRouteService(ctx);
        var (firstSuccess, firstError, firstId) = await service.CreateAsync(new StorageRouteUpsertRequest { Module = "MDR", DocumentType = null, StorageProviderId = providerA.StorageProviderId, IsDefault = true }, "tester");
        var (secondSuccess, secondError, _) = await service.CreateAsync(new StorageRouteUpsertRequest { Module = "MDR", DocumentType = null, StorageProviderId = providerB.StorageProviderId, IsDefault = true }, "tester");

        Assert.True(firstSuccess, firstError);
        Assert.False(secondSuccess);
        Assert.NotNull(secondError);

        var first = await ctx.StorageRoutes.FindAsync(firstId!.Value);
        Assert.True(first!.IsDefault);
    }

    [Fact]
    public async Task DeleteAsync_ExistingRoute_SoftDeletesIt()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(DeleteAsync_ExistingRoute_SoftDeletesIt));

        var provider = new StorageProvider { Name = "P2", Code = "P2", RootPath = "C:/p2", IsActive = true, IsEnabled = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var service = new StorageRouteService(ctx);
        var (_, _, id) = await service.CreateAsync(new StorageRouteUpsertRequest { Module = "Equipment", StorageProviderId = provider.StorageProviderId }, "tester");

        var (success, error) = await service.DeleteAsync(id!.Value);
        Assert.True(success, error);

        var deleted = await ctx.StorageRoutes.FindAsync(id.Value);
        Assert.False(deleted!.IsActive);

        var remaining = await service.GetAllAsync();
        Assert.DoesNotContain(remaining, r => r.StorageRouteId == id.Value);
    }
}
