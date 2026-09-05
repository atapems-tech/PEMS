namespace PEMS.Application.Interfaces;

/// <summary>
/// پاک‌سازی HTML تولیدشده توسط ادیتور متن غنی (Rich Text Editor) پیش از ذخیره - جلوگیری از
/// XSS. طراحی به‌گونه‌ای که هر ماژول دیگری (Meeting Minutes، Task Description و...) هم بتواند
/// از همین سرویس استفاده کند.
/// </summary>
public interface IRichTextSanitizerService
{
    string Sanitize(string? html);

    // برای PDF/Export که فرمت غنی پشتیبانی نمی‌شود - متن ساده بدون تگ HTML
    string ToPlainText(string? html);
}
