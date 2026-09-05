using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// ==============================================================================
/// Engineering Copilot - طبق فاز ۴ نقشه‌راه معماری: گسترش زیرساخت Semantic Search/Knowledge
/// Base موجود (Ollama) به سه قابلیت مشخص که در نقشه‌راه قول داده شده بود:
///   ۱. پیشنهاد MOC بر اساس الگوی NCR های تکراری (قبلاً کاملاً غایب بود)
///   ۲. تحلیل علت تاخیر با داده‌ی واقعی سیستم - نه فرضی (قبلاً کاملاً غایب بود)
///   ۳. کشف روابط معنایی بین مدارک (تکمیل تشخیص متن‌محور موجود در IntakeService)
///
/// طبق همون اصل همیشگی: اگر Ollama در دسترس نباشد، این سرویس نباید هیچ صفحه‌ای را خراب
/// کند - فقط نتیجه‌ی خالی/پیام مناسب برمی‌گرداند.
/// ==============================================================================
/// </summary>
public interface IEngineeringCopilotService
{
    Task<List<RecurringNcrPatternDto>> GetRecurringNcrPatternsAsync(int? projectId = null, int minOccurrences = 3, int lookbackDays = 180);

    Task<DelayRootCauseAnalysisDto> GetDelayRootCauseAnalysisAsync(int? projectId = null);

    // کشف رابطه‌ی معنایی این مدرک با بقیه‌ی مدارک (نه فقط تطبیق متن دقیق شماره‌مدرک/تگ که
    // در IntakeService از قبل هست) - نتیجه با AddAIProposedRelationshipAsync ثبت می‌شود
    Task<int> AnalyzeDocumentRelationshipsAsync(int documentId);
}
