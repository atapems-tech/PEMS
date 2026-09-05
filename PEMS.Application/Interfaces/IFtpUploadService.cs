namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۵.۲: آپلود مدارک فروشنده روی FTP
/// Server تنظیم‌شده توسط CONTRACTOR. طبق تصمیم کاربر (اپلیکیشن کاملاً آفلاین است): بخش
/// اطلاع‌رسانی ایمیلی سند پیاده نشده - به‌جایش خودِ اپلیکیشن، آپلود موفق را به‌عنوان تأییدیه
/// داخلی ثبت می‌کند (نه ایمیل خارجی).
/// </summary>
public interface IFtpUploadService
{
    /// <summary>
    /// فایل را در پوشه‌ی remoteFolder (نسبت به BaseRemotePath تنظیمات) آپلود می‌کند و مسیر کامل
    /// remote را برمی‌گرداند. اگر تنظیمات FTP (Host/Username) خالی باشد، خطای قابل‌فهم می‌دهد.
    /// </summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string remoteFolder);

    bool IsConfigured { get; }
}
