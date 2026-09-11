using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۸: ترتیب تفکیک مسیریابی -
/// ۱) Route فعال دقیق برای Module+DocumentType
/// ۲) Route فعال Module-Default (DocumentType=null, IsDefault=true) برای همان Module
/// ۳) StorageProvider سراسری پیش‌فرض (IsDefault=true, IsEnabled=true)
/// اگر هیچ‌کدام یافت نشود، StorageException(StorageConfigurationInvalid) پرتاب می‌شود - هرگز
/// حدس زده نمی‌شود.
/// </summary>
public interface IStorageRouteResolver
{
    Task<StorageProvider> ResolveAsync(string module, string? documentType = null, CancellationToken cancellationToken = default);
}
