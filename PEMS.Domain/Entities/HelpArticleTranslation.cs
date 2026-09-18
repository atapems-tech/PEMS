using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۳.۱ سند: هر مقاله‌ی راهنما دقیقاً دو ترجمه دارد (fa/en) - هر دو از ابتدا
/// درجه‌یک، نه این‌که انگلیسی اصل باشد و فارسی بعداً اضافه شود (یا برعکس). Content به‌صورت
/// HTML پاک‌سازی‌شده ذخیره می‌شود (IRichTextSanitizerService موجود پروژه - همان سرویسی که
/// برای ادیتور متن غنی جاهای دیگر استفاده می‌شود؛ کتابخانه‌ی جدیدی اضافه نشد).
/// </summary>
public class HelpArticleTranslation : BaseEntity
{
    public int HelpArticleTranslationId { get; set; }

    public int HelpArticleId { get; set; }
    public HelpArticle? Article { get; set; }

    // "fa" | "en"
    public string LanguageCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    // HTML پاک‌سازی‌شده (نه Markdown - طبق بخش ۷.۱ سند، یکی از گزینه‌های مجاز؛ چون
    // IRichTextSanitizerService از قبل در پروژه برای همین منظور آماده است)
    public string Content { get; set; } = string.Empty;

    // کلیدواژه‌های جستجو - جدا از متن اصلی، برای منطبق‌کردن اصطلاحات رایج/مخفف‌ها
    // (مثلاً "MDR" حتی اگر در متن اصلی نیامده باشد)
    public string? SearchKeywords { get; set; }
}
