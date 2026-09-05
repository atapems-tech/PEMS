namespace PEMS.Application.Interfaces;

/// <summary>
/// شماره‌گذاری خودکار ترانسمیتال طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۳:
/// فرمت TV-X-UU-AAAA-NNNN (X=کد دپارتمان دیسیپلین، UU=کد واحد پروژه، AAAA=۴ رقم آخر شماره‌ی
/// MR، NNNN=سریال اتمیک). فقط برای ترانسمیتال‌های مرتبط با یک MR کاربرد دارد؛ ترانسمیتال‌های
/// عمومی (بدون MR) همچنان شماره‌ی دستی می‌گیرند.
/// </summary>
public interface ITransmittalNumberingService
{
    Task<string> GenerateNextNumberAsync(int mrId);
}
