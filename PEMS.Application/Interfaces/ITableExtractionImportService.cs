using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Stage-سپس-تایید برای ردیف‌های استخراج‌شده از جدول: هیچ ردیفی مستقیم Equipment/Instrument
/// نمی‌سازد. تطبیق با Equipment فقط یک پیشنهاد است؛ نوشتن واقعی فقط در ConfirmRowAsync و فقط
/// با تایید صریح کاربر اتفاق می‌افتد (همان قاعده‌ی RelationshipService.ConfirmSuggestedLinkAsync).
/// </summary>
public interface ITableExtractionImportService
{
    Task<int> StageAsync(int attachmentId, string extractionMethod, List<TableExtractionRowInput> rows, int? createdById);

    // equipmentId: پیشنهاد تطبیق خودکار یا انتخاب دستی کاربر - هر دو حالت از همین یک متد رد می‌شوند
    Task ConfirmRowAsync(int rowId, int equipmentId, int? reviewedById);

    Task RejectRowAsync(int rowId, int? reviewedById, string? comment);

    // برای ردیف‌هایی که از قبل با تطبیق خودکار یک Equipment پیدا کرده‌اند - طبق همان الگوی
    // RelationshipService.BulkConfirmAsync (هر ردیف جدا Confirm می‌شود، خطای یکی مانع بقیه نمی‌شود)
    Task<(int Confirmed, List<string> Errors)> BulkConfirmMatchedAsync(List<int> rowIds, int? reviewedById);
}
