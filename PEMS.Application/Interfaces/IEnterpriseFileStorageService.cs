using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲: نمای کسب‌وکاری جدید و چند-Provider -
/// ماژول‌ها فقط "چه فایلی" را می‌دانند، نه "کجا فیزیکی ذخیره می‌شود". این Interface کاملاً
/// افزودنی است و IFileStorageService موجود (که ~۸ کنترلر واقعی امروز از آن استفاده می‌کنند) را
/// جایگزین نمی‌کند - آن‌ها دست‌نخورده به کار خود ادامه می‌دهند تا زمانی که آگاهانه به این
/// سرویس جدید مهاجرت داده شوند (طبق بند ۲۲: مهاجرت اجباری این فاز نیست).
///
/// مسیر فیزیکی همیشه قطعی و امن تولید می‌شود: {Module}/{Year}/{EntityId?}/{Guid}{Ext} - هرگز
/// از نام فایل ورودی کاربر مشتق نمی‌شود (بند ۲۰).
/// </summary>
public interface IEnterpriseFileStorageService
{
    Task<EnterpriseStorageResultDto> SaveAsync(
        string module, string? documentType, int? entityId,
        string originalFileName, Stream content, string? createdBy,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(int storageProviderId, string relativePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int storageProviderId, string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(int storageProviderId, string relativePath, CancellationToken cancellationToken = default);
}
