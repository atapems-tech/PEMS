using Microsoft.AspNetCore.DataProtection;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Infrastructure.FileStorage;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲۹: تست مدیریت ادمین StorageProvider -
/// یکتایی Default (فقط یکی می‌تواند IsDefault=true باشد)، رد پیکربندی نامعتبر، و مسدودسازی حذف
/// وقتی یک Route فعال به این Provider ارجاع دارد.
/// </summary>
public class StorageProviderServiceTests
{
    private static ICredentialProtector CreateProtector() =>
        new CredentialProtector(new EphemeralDataProtectionProvider());

    private static StorageProviderService CreateService(PEMS.Persistence.Context.PEMSDbContext ctx) =>
        new(ctx, CreateProtector(), new StorageProviderClientFactory(CreateProtector()));

    [Fact]
    public async Task SetDefaultAsync_AnotherProviderAlreadyDefault_ClearsOldDefaultAndSetsNew()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(SetDefaultAsync_AnotherProviderAlreadyDefault_ClearsOldDefaultAndSetsNew));

        var first = new StorageProvider { Name = "First", Code = "F1", RootPath = "C:/f1", IsEnabled = true, IsActive = true, IsDefault = true };
        var second = new StorageProvider { Name = "Second", Code = "F2", RootPath = "C:/f2", IsEnabled = true, IsActive = true };
        ctx.StorageProviders.AddRange(first, second);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var (success, error) = await service.SetDefaultAsync(second.StorageProviderId, "tester");

        Assert.True(success, error);
        Assert.False((await ctx.StorageProviders.FindAsync(first.StorageProviderId))!.IsDefault);
        Assert.True((await ctx.StorageProviders.FindAsync(second.StorageProviderId))!.IsDefault);
    }

    [Fact]
    public async Task SetDefaultAsync_ProviderIsDisabled_Fails()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(SetDefaultAsync_ProviderIsDisabled_Fails));

        var disabled = new StorageProvider { Name = "Disabled", Code = "DIS1", RootPath = "C:/dis1", IsEnabled = false, IsActive = true };
        ctx.StorageProviders.Add(disabled);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var (success, error) = await service.SetDefaultAsync(disabled.StorageProviderId, "tester");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_InvalidProviderType_ReturnsValidationError()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_InvalidProviderType_ReturnsValidationError));
        var service = CreateService(ctx);

        var (success, error, id) = await service.CreateAsync(new StorageProviderUpsertRequest
        {
            Name = "Bad", Code = "BAD1", ProviderType = "SomeUnknownType", RootPath = "C:/bad"
        }, "tester");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(id);
    }

    [Fact]
    public async Task CreateAsync_FtpTypeMissingHost_ReturnsValidationError()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_FtpTypeMissingHost_ReturnsValidationError));
        var service = CreateService(ctx);

        var (success, error, _) = await service.CreateAsync(new StorageProviderUpsertRequest
        {
            Name = "FtpNoHost", Code = "FTPNH", ProviderType = StorageProviderTypes.Ftp, RootPath = "/remote"
        }, "tester");

        Assert.False(success);
        Assert.Contains("Host", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ReturnsValidationError()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_DuplicateCode_ReturnsValidationError));
        var service = CreateService(ctx);

        await service.CreateAsync(new StorageProviderUpsertRequest { Name = "Original", Code = "DUP", ProviderType = StorageProviderTypes.Local, RootPath = "C:/one" }, "tester");
        var (success, error, _) = await service.CreateAsync(new StorageProviderUpsertRequest { Name = "Duplicate", Code = "DUP", ProviderType = StorageProviderTypes.Local, RootPath = "C:/two" }, "tester");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task CreateAsync_ValidLocalProvider_PasswordNeverStoredPlaintext()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(CreateAsync_ValidLocalProvider_PasswordNeverStoredPlaintext));
        var service = CreateService(ctx);

        var (success, error, id) = await service.CreateAsync(new StorageProviderUpsertRequest
        {
            Name = "SecureFtp", Code = "SECFTP", ProviderType = StorageProviderTypes.Ftp,
            Host = "ftp.example.com", Username = "svc", Password = "S3cretPass!", RootPath = "/root"
        }, "tester");

        Assert.True(success, error);
        var stored = await ctx.StorageProviders.FindAsync(id!.Value);
        Assert.NotNull(stored!.EncryptedPassword);
        Assert.DoesNotContain("S3cretPass!", stored.EncryptedPassword);
    }

    [Fact]
    public async Task DeleteAsync_ProviderReferencedByActiveRoute_IsBlocked()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(DeleteAsync_ProviderReferencedByActiveRoute_IsBlocked));

        var provider = new StorageProvider { Name = "InUse", Code = "INUSE", RootPath = "C:/inuse", IsEnabled = true, IsActive = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        ctx.StorageRoutes.Add(new StorageRoute { Module = "VDR", StorageProviderId = provider.StorageProviderId, IsEnabled = true, IsActive = true });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var (success, error) = await service.DeleteAsync(provider.StorageProviderId);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.True((await ctx.StorageProviders.FindAsync(provider.StorageProviderId))!.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_UnreferencedNonDefaultProvider_Succeeds()
    {
        await using var ctx = PemsDbContextFactory.CreateInMemory(nameof(DeleteAsync_UnreferencedNonDefaultProvider_Succeeds));

        var provider = new StorageProvider { Name = "Unused", Code = "UNUSED", RootPath = "C:/unused", IsEnabled = true, IsActive = true };
        ctx.StorageProviders.Add(provider);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var (success, error) = await service.DeleteAsync(provider.StorageProviderId);

        Assert.True(success, error);
        Assert.False((await ctx.StorageProviders.FindAsync(provider.StorageProviderId))!.IsActive);
    }
}
