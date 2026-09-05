using PEMS.Application.AI.Models;

namespace PEMS.Application.AI.Interfaces;

public interface IDocumentAIService
{
    Task<AIAnalysisResult> AnalyzeDocumentAsync(string filePath, string fileExtension);

    // برای مدارک جدولی (Wiring Connection Diagram, Instrument Index, Marshalling List, I/O List,
    // Loop Drawing) - پرامپت مخصوص ردیف‌های جدول را با Chunking واقعی روی مرز خط اجرا می‌کند
    // تا هیچ ردیفی وسط بریده نشود، و نتایج/هشدارهای همه‌ی چانک‌ها را ادغام می‌کند.
    Task<AIAnalysisResult> AnalyzeTableDocumentAsync(string filePath, string fileExtension, string? documentHint = null);
}
