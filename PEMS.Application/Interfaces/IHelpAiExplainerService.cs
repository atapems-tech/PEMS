namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۱۱ (AI Integration Readiness): این
/// اینترفیس مرز یکپارچگی با موتور AI موجود پروژه (IOllamaService، همان موتوری که
/// ChatAssistantService استفاده می‌کند) است - نه یک موتور AI جدید. پاسخ همیشه محدود به محتوای
/// واقعیِ خودِ مقاله‌ی راهنما است (الگوی «answer using ONLY the data below» که در
/// ChatAssistantService هم استفاده شده)، تا AI هرگز مجوز/وضعیت/گردش‌کاری که در سیستم واقعی
/// وجود ندارد را اختراع نکند.
/// </summary>
public interface IHelpAiExplainerService
{
    /// <summary>
    /// helpArticleCode: مقاله‌ای که کاربر همین الان باز کرده (زمینه‌ی پاسخ) - اختیاری. اگر
    /// داده نشود، پاسخ صرفاً بر مبنای دانش عمومیِ محدودشده به قوانین صریح PEMS (نه حدس) است.
    /// </summary>
    Task<string> ExplainAsync(string? helpArticleCode, string question, string languageCode, int? userId);
}
