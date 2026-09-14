using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System»: هسته‌ی مشترک شناسایی/چرخه‌عمر/Resolve برای کل PEMS -
/// یک IBarcodeService واحد و قابل‌گسترش (نه EquipmentBarcodeService/WarehouseBarcodeService جدا).
/// </summary>
public interface IBarcodeService
{
    /// <summary>
    /// طبق بند «Duplicate Protection»/«Idempotent generation»: اگر یک بارکد Active از قبل برای
    /// همین TargetType+TargetId وجود دارد، همان بازگردانده می‌شود - رکورد دوم ساخته نمی‌شود.
    /// </summary>
    Task<(BarcodeIdentityDto Barcode, bool WasExisting)> GenerateAsync(string targetType, int targetId, string symbology, int? createdById);

    Task<BulkGenerateResultDto> BulkGenerateAsync(string targetType, List<int> targetIds, string symbology, int? createdById);

    Task<BarcodeIdentityDto?> GetByIdAsync(int barcodeIdentityId);

    Task<BarcodeIdentityDto?> GetActiveByTargetAsync(string targetType, int targetId);

    Task<List<BarcodeIdentityDto>> GetHistoryByTargetAsync(string targetType, int targetId);

    /// <summary>
    /// طبق بند «Barcode Resolution»: فقط Decode+اعتبارسنجی+حداقل هویت - بدون بارگذاری رکورد کامل
    /// هدف و بدون تصمیم Authorization (که مسئولیت لایه‌ی بالاتر/Controller است).
    /// </summary>
    Task<BarcodeResolutionResultDto> ResolveAsync(string code);

    Task<(bool Success, string? Error)> RecordPrintAsync(int barcodeIdentityId);

    Task<(bool Success, string? Error)> DisableAsync(int barcodeIdentityId, string? reason, int? disabledById);

    Task<(bool Success, string? Error)> RevokeAsync(int barcodeIdentityId, string? reason, int? revokedById);

    /// <summary>
    /// طبق بند «Replace»: بارکد قدیمی Revoked می‌شود و یک بارکد جدید برای همان رکورد کسب‌وکاری
    /// ساخته می‌شود - زنجیره‌ی قدیمی→جدید برای Audit نگه داشته می‌شود.
    /// </summary>
    Task<(BarcodeIdentityDto? NewBarcode, string? Error)> ReplaceAsync(int oldBarcodeIdentityId, string symbology, int? createdById);
}
