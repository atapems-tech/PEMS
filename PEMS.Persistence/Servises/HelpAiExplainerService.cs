using Microsoft.EntityFrameworkCore;
using PEMS.AI.Interfaces;
using PEMS.AI.Models;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۱۱: پیاده‌سازی واقعی «Ask PEMS AI» - نه
/// یک ویژگی نمایشی. دقیقاً همان الگوی ChatAssistantService را برای IOllamaService تکرار
/// می‌کند (پرامپت محدود به «فقط از داده‌ی زیر پاسخ بده»)، اما زمینه‌ی پاسخ به‌جای عکس‌فوری کلی
/// سیستم، محتوای واقعیِ همان مقاله‌ی راهنمایی است که کاربر باز کرده - تا AI هرگز مجوز/وضعیت/
/// گردش‌کاری که در PEMS واقعی وجود ندارد را اختراع نکند (طبق محدودیت صریح سند).
/// </summary>
public class HelpAiExplainerService : IHelpAiExplainerService
{
    private readonly PEMSDbContext _context;
    private readonly IOllamaService _ollamaService;

    public HelpAiExplainerService(PEMSDbContext context, IOllamaService ollamaService)
    {
        _context = context;
        _ollamaService = ollamaService;
    }

    public async Task<string> ExplainAsync(string? helpArticleCode, string question, string languageCode, int? userId)
    {
        if (string.IsNullOrWhiteSpace(question))
            return languageCode == "fa" ? "لطفاً سوال خود را بنویسید." : "Please enter a question.";

        string context;

        if (!string.IsNullOrWhiteSpace(helpArticleCode))
        {
            var article = await _context.HelpArticles
                .Include(a => a.Translations)
                .FirstOrDefaultAsync(a => a.Code == helpArticleCode && a.IsPublished);

            if (article != null)
            {
                var en = article.Translations.FirstOrDefault(t => t.LanguageCode == "en");
                var fa = article.Translations.FirstOrDefault(t => t.LanguageCode == "fa");

                context = $"""
                    Help article: {article.Code} (type: {article.ArticleType}, context key: {article.HelpKey ?? "n/a"})
                    -- English --
                    Title: {en?.Title}
                    Summary: {en?.Summary}
                    Content: {StripHtml(en?.Content)}
                    -- Persian --
                    Title: {fa?.Title}
                    Summary: {fa?.Summary}
                    Content: {StripHtml(fa?.Content)}
                    """;
            }
            else
            {
                context = "No matching Help article was found for the given context.";
            }
        }
        else
        {
            context = "No specific Help article is open right now; answer using only general, already-documented PEMS Help concepts if you are certain, otherwise say you don't have that documented yet.";
        }

        var prompt = $"""
            You are the PEMS Help Assistant. Answer the user's question using ONLY the Help
            article content below. Do not invent permissions, statuses, workflow steps, or
            business rules that are not stated in the content. If the content does not answer
            the question, say clearly (in the user's language) that this is not yet documented
            in PEMS Help, rather than guessing. Be concise (max 6 sentences). Answer in the same
            language as the question ({(languageCode == "fa" ? "Persian" : "English")} preferred
            if the question language is ambiguous).

            HELP CONTENT:
            {context}

            QUESTION:
            {question}
            """;

        try
        {
            var answer = await _ollamaService.GenerateAsync(prompt, new OllamaGenerateOptions { Temperature = 0.2, MaxResponseTokens = 300 });
            return string.IsNullOrWhiteSpace(answer)
                ? (languageCode == "fa" ? "پاسخی از موتور هوش مصنوعی دریافت نشد. لطفاً دوباره تلاش کنید." : "No response was received from the AI engine. Please try again.")
                : answer;
        }
        catch (Exception ex)
        {
            return languageCode == "fa"
                ? $"متاسفانه در حال حاضر امکان ارتباط با موتور هوش مصنوعی محلی (Ollama) وجود ندارد. جزئیات فنی: {ex.Message}"
                : $"The local AI engine (Ollama) is currently unreachable. Technical detail: {ex.Message}";
        }
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
    }
}
