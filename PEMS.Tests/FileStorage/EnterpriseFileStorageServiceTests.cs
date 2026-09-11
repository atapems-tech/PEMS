using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using PEMS.Application.DTOs;
using PEMS.Application.Exceptions;
using PEMS.Domain.Entities;
using PEMS.Infrastructure.FileStorage;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲۰/۲۱/۲۹: تست end-to-end
/// EnterpriseFileStorageService - مسیر قطعی و امن، محاسبه‌ی درست SHA-256 حین Streaming (بدون
/// خواندن دوباره‌ی فایل)، و رد صریح درخواست وقتی Provider یافت نشود یا غیرفعال باشد.
/// </summary>
public class EnterpriseFileStorageServiceTests : IDisposable
{
    private readonly string _root;

    public EnterpriseFileStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pems_enterprise_storage_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private EnterpriseFileStorageService CreateService(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());
        var clientFactory = new StorageProviderClientFactory(protector);
        var resolver = new StorageRouteResolver(ctx);
        return new EnterpriseFileStorageService(resolver, clientFactory, ctx);
    }

    [Fact]
    public async Task SaveAsync_ComputesCorrectSha256AndDeterministicPath()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(SaveAsync_ComputesCorrectSha256AndDeterministicPath));

        var provider = new StorageProvider { Name = "Local", Code = "LOC1", ProviderType = StorageProviderTypes.Local, RootPath = _root, IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var fileBytes = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog");
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(fileBytes));

        var service = CreateService(ctx);
        EnterpriseStorageResultDto result;
        await using (var content = new MemoryStream(fileBytes))
            result = await service.SaveAsync("EngineeringDocuments", "Drawing", 42, "malicious name ../../etc.txt", content, "tester");

        Assert.Equal(expectedHash, result.Sha256);
        Assert.Equal(fileBytes.Length, result.FileSizeBytes);
        Assert.Equal(provider.StorageProviderId, result.StorageProviderId);
        Assert.Equal("LOC1", result.StorageProviderCode);

        // مسیر باید قطعی و امن باشد: {Module}/{Year}/{EntityId}/{Guid}{Ext} - هرگز نام فایل ورودی خام نیست
        Assert.StartsWith($"EngineeringDocuments/{DateTime.Now.Year}/42/", result.RelativePath);
        Assert.DoesNotContain("malicious", result.RelativePath);
        Assert.DoesNotContain("..", result.RelativePath);
        Assert.EndsWith(".txt", result.RelativePath);

        Assert.True(await service.ExistsAsync(provider.StorageProviderId, result.RelativePath));
    }

    [Fact]
    public async Task SaveAsync_TwoFilesWithSameOriginalName_ProduceDifferentStoredPaths()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(SaveAsync_TwoFilesWithSameOriginalName_ProduceDifferentStoredPaths));

        var provider = new StorageProvider { Name = "Local", Code = "LOC2", ProviderType = StorageProviderTypes.Local, RootPath = _root, IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        EnterpriseStorageResultDto first, second;
        await using (var c1 = new MemoryStream(Encoding.UTF8.GetBytes("content-1")))
            first = await service.SaveAsync("VDR", null, null, "duplicate.txt", c1, "tester");
        await using (var c2 = new MemoryStream(Encoding.UTF8.GetBytes("content-2")))
            second = await service.SaveAsync("VDR", null, null, "duplicate.txt", c2, "tester");

        Assert.NotEqual(first.RelativePath, second.RelativePath);
    }

    [Fact]
    public async Task OpenReadAsync_ProviderNotFound_ThrowsStorageProviderNotFound()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(OpenReadAsync_ProviderNotFound_ThrowsStorageProviderNotFound));
        var service = CreateService(ctx);

        var ex = await Assert.ThrowsAsync<StorageException>(() => service.OpenReadAsync(9999, "whatever.txt"));
        Assert.Equal(StorageErrorCodes.StorageProviderNotFound, ex.Code);
    }

    [Fact]
    public async Task ExistsAsync_ProviderDisabled_ThrowsStorageProviderDisabled()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(ExistsAsync_ProviderDisabled_ThrowsStorageProviderDisabled));

        var provider = new StorageProvider { Name = "Disabled", Code = "DISX", ProviderType = StorageProviderTypes.Local, RootPath = _root, IsEnabled = false, IsActive = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var ex = await Assert.ThrowsAsync<StorageException>(() => service.ExistsAsync(provider.StorageProviderId, "whatever.txt"));

        Assert.Equal(StorageErrorCodes.StorageProviderDisabled, ex.Code);
    }

    [Fact]
    public async Task DeleteAsync_SavedFile_ActuallyRemovesIt()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(DeleteAsync_SavedFile_ActuallyRemovesIt));

        var provider = new StorageProvider { Name = "Local", Code = "LOC3", ProviderType = StorageProviderTypes.Local, RootPath = _root, IsEnabled = true, IsActive = true, IsDefault = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        EnterpriseStorageResultDto result;
        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("to delete")))
            result = await service.SaveAsync("Contracts", null, null, "x.txt", content, "tester");

        Assert.True(await service.ExistsAsync(provider.StorageProviderId, result.RelativePath));
        await service.DeleteAsync(provider.StorageProviderId, result.RelativePath);
        Assert.False(await service.ExistsAsync(provider.StorageProviderId, result.RelativePath));
    }
}
