namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق ممیزی امنیتی (P0 - Engineering File Security Containment): جایگزین کد پراکنده‌ی
/// Path.Combine(wwwroot, "uploads", ...) که در ~۹ Controller تکرار شده بود - فایل‌های کسب‌وکاری
/// دیگر هرگز مستقیم زیر wwwroot ذخیره نمی‌شوند (که با UseStaticFiles بدون احراز هویت در دسترس
/// همه بود). Key برگشتی یک رشته‌ی نسبی است (نه مسیر فیزیکی مطلق، نه URL عمومی) - تنها از طریق
/// این سرویس قابل تبدیل به بایت واقعی است. طراحی به‌گونه‌ای که جایگزینی بعدی با Object Storage
/// (S3/Azure Blob) نیازی به تغییر منطق کسب‌وکار/Domain نداشته باشد.
/// </summary>
public interface IFileStorageService
{
    /// <summary>ذخیره‌ی محتوا؛ logicalFolder فقط برای سازمان‌دهی است (مثل پوشه‌بندی امروز:
    /// vdr/comments، letters، moc، ...) - نام فایل فیزیکی همیشه GUID تولیدشده توسط خودِ سرویس
    /// است، هرگز از ورودی کاربر مشتق نمی‌شود (جلوگیری ساختاری از Path Traversal).</summary>
    Task<string> SaveAsync(string logicalFolder, string originalFileName, Stream content);

    Task<Stream> OpenReadAsync(string key);

    Task<bool> ExistsAsync(string key);

    Task DeleteAsync(string key);
}
