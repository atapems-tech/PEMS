using PEMS.Application.Exceptions;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Infrastructure.FileStorage;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۷: تنها نقطه‌ای که تصمیم می‌گیرد کدام
/// IStorageProviderClient واقعی بر اساس StorageProvider.ProviderType ساخته شود؛ رمزگشایی
/// EncryptedPassword هم فقط همین‌جا انجام می‌شود - رمز عبور رمزگشایی‌شده هرگز از این کلاس
/// بیرون نمی‌رود (نه در DTO، نه در لاگ).
/// </summary>
public class StorageProviderClientFactory : IStorageProviderClientFactory
{
    private readonly ICredentialProtector _credentialProtector;

    public StorageProviderClientFactory(ICredentialProtector credentialProtector)
    {
        _credentialProtector = credentialProtector;
    }

    public IStorageProviderClient Create(StorageProvider provider)
    {
        var password = string.IsNullOrEmpty(provider.EncryptedPassword)
            ? string.Empty
            : _credentialProtector.Unprotect(provider.EncryptedPassword);

        return provider.ProviderType switch
        {
            StorageProviderTypes.Local or StorageProviderTypes.NetworkShare =>
                new FileSystemStorageProviderClient(provider.RootPath),

            StorageProviderTypes.Ftp =>
                new FtpStorageProviderClient(
                    provider.Host ?? string.Empty, provider.Port ?? 21, provider.Username ?? string.Empty,
                    password, provider.RootPath, provider.UseSsl, provider.UsePassiveMode),

            StorageProviderTypes.Sftp =>
                new SftpStorageProviderClient(
                    provider.Host ?? string.Empty, provider.Port ?? 22, provider.Username ?? string.Empty,
                    password, provider.RootPath),

            _ => throw new StorageException(StorageErrorCodes.StorageConfigurationInvalid,
                $"نوع Provider «{provider.ProviderType}» پشتیبانی نمی‌شود.")
        };
    }
}
