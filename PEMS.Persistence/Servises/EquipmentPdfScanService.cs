using System.Text.RegularExpressions;
using PEMS.Application.AI.Parser;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق درخواست: تشخیص تگ تجهیز از PDF. متن سند را (با همان موتور OCR/PdfPig موجود پروژه)
/// استخراج می‌کند و دنبال رشته‌هایی می‌گردد که شبیه الگوی رایج تگ تجهیزات صنعتی هستند
/// (مثل 80-PSH-601305 یا 88-B-9001 - دقیقاً همان الگویی که در نمونه‌های واقعی شما دیده شد).
/// نتیجه یک لیست کاندید برای بازبینی کاربر است، نه استخراج تضمین‌شده‌ی کامل.
/// </summary>
public class EquipmentPdfScanService : IEquipmentPdfScanService
{
    // الگوی مشاهده‌شده در نمونه‌های واقعی: کد ناحیه (۲ تا ۳ رقم) - کد نوع تجهیز (۱ تا ۵ حرف) -
    // شماره ترتیبی (۳ تا ۶ رقم) + پسوند حرفی اختیاری
    private static readonly Regex TagPattern = new(
        @"\b\d{2,3}-[A-Z]{1,5}-\d{3,6}[A-Z]?\b",
        RegexOptions.Compiled);

    private readonly IEnumerable<ITextExtractor> _extractors;

    public EquipmentPdfScanService(IEnumerable<ITextExtractor> extractors)
    {
        _extractors = extractors;
    }

    public async Task<List<string>> ScanForTagCandidatesAsync(string filePath)
    {
        var extractor = _extractors.FirstOrDefault(x => x.CanHandle(".pdf"));
        if (extractor == null) return new List<string>();

        var extracted = await extractor.ExtractAsync(filePath);

        if (string.IsNullOrWhiteSpace(extracted.FullText))
            return new List<string>();

        var matches = TagPattern.Matches(extracted.FullText)
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return matches;
    }
}
