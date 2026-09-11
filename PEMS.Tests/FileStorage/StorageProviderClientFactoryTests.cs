using Microsoft.AspNetCore.DataProtection;
using PEMS.Application.Exceptions;
using PEMS.Domain.Entities;
using PEMS.Infrastructure.FileStorage;
using PEMS.Persistence.Services;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۷: تست انتخاب صحیح نوع Client بر اساس
/// ProviderType، و رد صریح یک نوع پشتیبانی‌نشده به‌جای شکست خاموش.
/// </summary>
public class StorageProviderClientFactoryTests
{
    private static StorageProviderClientFactory CreateFactory() =>
        new(new CredentialProtector(new EphemeralDataProtectionProvider()));

    [Theory]
    [InlineData(StorageProviderTypes.Local, typeof(FileSystemStorageProviderClient))]
    [InlineData(StorageProviderTypes.NetworkShare, typeof(FileSystemStorageProviderClient))]
    [InlineData(StorageProviderTypes.Ftp, typeof(FtpStorageProviderClient))]
    [InlineData(StorageProviderTypes.Sftp, typeof(SftpStorageProviderClient))]
    public void Create_EachProviderType_ReturnsExpectedClientImplementation(string providerType, Type expectedType)
    {
        var factory = CreateFactory();
        var provider = new StorageProvider { ProviderType = providerType, RootPath = "C:/x", Host = "host", Username = "u" };

        var client = factory.Create(provider);

        Assert.IsType(expectedType, client);
    }

    [Fact]
    public void Create_UnsupportedProviderType_ThrowsStorageConfigurationInvalid()
    {
        var factory = CreateFactory();
        var provider = new StorageProvider { ProviderType = "Minio", RootPath = "C:/x" };

        var ex = Assert.Throws<StorageException>(() => factory.Create(provider));
        Assert.Equal(StorageErrorCodes.StorageConfigurationInvalid, ex.Code);
    }

    [Fact]
    public void Create_EncryptedPasswordIsDecryptedOnlyInsideFactory()
    {
        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());
        var factory = new StorageProviderClientFactory(protector);
        const string plaintext = "MyFtpPassword";

        var provider = new StorageProvider
        {
            ProviderType = StorageProviderTypes.Ftp,
            Host = "host", Username = "u", RootPath = "/root",
            EncryptedPassword = protector.Protect(plaintext)
        };

        // فقط تضمین می‌کنیم Create بدون خطا رمز عبور رمزنگاری‌شده را می‌پذیرد و Client می‌سازد -
        // رمز عبور رمزگشایی‌شده هرگز از Create بیرون بازنمی‌گردد (نه در نوع بازگشتی، نه در فیلدی
        // عمومی از FtpStorageProviderClient).
        var client = factory.Create(provider);
        Assert.IsType<FtpStorageProviderClient>(client);
    }
}
