using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۷: قرارداد مشترک بین همه‌ی انواع
/// Provider (Local/NetworkShare/Ftp/Sftp/آینده Minio/S3). ماژول‌های کسب‌وکاری هرگز این
/// Interface را مستقیم نمی‌بینند - فقط از طریق IEnterpriseFileStorageService. relativePath
/// همیشه نسبت به RootPath همان Provider است، نه یک مسیر مطلق.
/// </summary>
public interface IStorageProviderClient
{
    Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    // طبق بند ۱۵: هرگز Exception خام از این متد بیرون نمی‌آید - همیشه یک نتیجه‌ی گام‌به‌گام
    Task<StorageHealthResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// طبق بند ۷: StorageProviderResolver/Factory - تنها نقطه‌ای که تصمیم می‌گیرد کدام
/// IStorageProviderClient واقعی (بر اساس StorageProvider.ProviderType) باید ساخته شود.
/// رمزگشایی EncryptedPassword هم همین‌جا (نه در Domain/DTO) انجام می‌شود.
/// </summary>
public interface IStorageProviderClientFactory
{
    IStorageProviderClient Create(StorageProvider provider);
}
