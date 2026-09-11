using PEMS.Application.Exceptions;
using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲۹: تست ترتیب صریح تفکیک Route
/// (Module+DocumentType دقیق → Module-Default → StorageProvider سراسری پیش‌فرض) و رفتار خطای
/// صریح StorageConfigurationInvalid وقتی هیچ‌کدام قابل استفاده نیست.
/// </summary>
public class StorageRouteResolverTests
{
    [Fact]
    public async Task ResolveAsync_ExactModuleAndDocumentTypeRouteExists_ReturnsItsProvider()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ResolveAsync_ExactModuleAndDocumentTypeRouteExists_ReturnsItsProvider));

        var specific = new StorageProvider { Name = "Specific", Code = "SPEC", RootPath = "C:/spec", IsEnabled = true, IsActive = true };
        var fallback = new StorageProvider { Name = "Fallback", Code = "FALL", RootPath = "C:/fall", IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.AddRange(specific, fallback);
        await ctx.SaveChangesAsync();

        ctx.StorageRoutes.Add(new StorageRoute { Module = "VDR", DocumentType = "Drawing", StorageProviderId = specific.StorageProviderId, IsEnabled = true, IsActive = true, Priority = 1 });
        await ctx.SaveChangesAsync();

        var resolver = new StorageRouteResolver(ctx);
        var result = await resolver.ResolveAsync("VDR", "Drawing");

        Assert.Equal(specific.StorageProviderId, result.StorageProviderId);
    }

    [Fact]
    public async Task ResolveAsync_NoExactRouteButModuleDefaultExists_ReturnsModuleDefaultProvider()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ResolveAsync_NoExactRouteButModuleDefaultExists_ReturnsModuleDefaultProvider));

        var moduleDefault = new StorageProvider { Name = "ModuleDefault", Code = "MODDEF", RootPath = "C:/mod", IsEnabled = true, IsActive = true };
        var globalDefault = new StorageProvider { Name = "GlobalDefault", Code = "GLOBDEF", RootPath = "C:/glob", IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.AddRange(moduleDefault, globalDefault);
        await ctx.SaveChangesAsync();

        ctx.StorageRoutes.Add(new StorageRoute { Module = "VDR", DocumentType = null, StorageProviderId = moduleDefault.StorageProviderId, IsEnabled = true, IsActive = true, IsDefault = true });
        await ctx.SaveChangesAsync();

        var resolver = new StorageRouteResolver(ctx);
        // درخواست یک DocumentType که هیچ Route دقیقی برایش وجود ندارد
        var result = await resolver.ResolveAsync("VDR", "SomeOtherType");

        Assert.Equal(moduleDefault.StorageProviderId, result.StorageProviderId);
    }

    [Fact]
    public async Task ResolveAsync_NoRoutesAtAll_FallsBackToGlobalDefaultProvider()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ResolveAsync_NoRoutesAtAll_FallsBackToGlobalDefaultProvider));

        var globalDefault = new StorageProvider { Name = "GlobalDefault", Code = "GLOBDEF2", RootPath = "C:/glob2", IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.Add(globalDefault);
        await ctx.SaveChangesAsync();

        var resolver = new StorageRouteResolver(ctx);
        var result = await resolver.ResolveAsync("Contracts");

        Assert.Equal(globalDefault.StorageProviderId, result.StorageProviderId);
    }

    [Fact]
    public async Task ResolveAsync_NothingConfiguredAtAll_ThrowsStorageConfigurationInvalid()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ResolveAsync_NothingConfiguredAtAll_ThrowsStorageConfigurationInvalid));

        var resolver = new StorageRouteResolver(ctx);
        var ex = await Assert.ThrowsAsync<StorageException>(() => resolver.ResolveAsync("Unconfigured"));

        Assert.Equal(StorageErrorCodes.StorageConfigurationInvalid, ex.Code);
    }

    [Fact]
    public async Task ResolveAsync_RouteProviderDisabled_FallsBackToGlobalDefault()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ResolveAsync_RouteProviderDisabled_FallsBackToGlobalDefault));

        var disabledProvider = new StorageProvider { Name = "Disabled", Code = "DIS", RootPath = "C:/dis", IsEnabled = false, IsActive = true };
        var globalDefault = new StorageProvider { Name = "GlobalDefault", Code = "GLOBDEF3", RootPath = "C:/glob3", IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.AddRange(disabledProvider, globalDefault);
        await ctx.SaveChangesAsync();

        ctx.StorageRoutes.Add(new StorageRoute { Module = "MDR", DocumentType = null, StorageProviderId = disabledProvider.StorageProviderId, IsEnabled = true, IsActive = true, IsDefault = true });
        await ctx.SaveChangesAsync();

        var resolver = new StorageRouteResolver(ctx);
        var result = await resolver.ResolveAsync("MDR");

        Assert.Equal(globalDefault.StorageProviderId, result.StorageProviderId);
    }

    [Fact]
    public async Task ResolveAsync_MultipleExactRoutesDifferentPriority_ReturnsHighestPriority()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ResolveAsync_MultipleExactRoutesDifferentPriority_ReturnsHighestPriority));

        var low = new StorageProvider { Name = "Low", Code = "LOW", RootPath = "C:/low", IsEnabled = true, IsActive = true };
        var high = new StorageProvider { Name = "High", Code = "HIGH", RootPath = "C:/high", IsEnabled = true, IsActive = true };
        ctx.StorageProviders.AddRange(low, high);
        await ctx.SaveChangesAsync();

        ctx.StorageRoutes.AddRange(
            new StorageRoute { Module = "Equipment", DocumentType = "Datasheet", StorageProviderId = low.StorageProviderId, IsEnabled = true, IsActive = true, Priority = 1 },
            new StorageRoute { Module = "Equipment", DocumentType = "Datasheet", StorageProviderId = high.StorageProviderId, IsEnabled = true, IsActive = true, Priority = 10 });
        await ctx.SaveChangesAsync();

        var resolver = new StorageRouteResolver(ctx);
        var result = await resolver.ResolveAsync("Equipment", "Datasheet");

        Assert.Equal(high.StorageProviderId, result.StorageProviderId);
    }
}
