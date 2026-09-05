using Microsoft.Extensions.Logging;
using PEMS.AI.Interfaces;
using PEMS.AI.Models;
using PEMS.Application.AI.Interfaces;
using PEMS.Application.AI.Models;
using PEMS.Application.AI.Parser;
using PEMS.Application.AI.Prompt;

namespace PEMS.Application.AI.Services;

public class DocumentAIService : IDocumentAIService
{
    private readonly IDocumentTextExtractorFactory _extractorFactory;
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<DocumentAIService> _logger;

    public DocumentAIService(
        IDocumentTextExtractorFactory extractorFactory,
        IOllamaService ollamaService,
        ILogger<DocumentAIService> logger)
    {
        _extractorFactory = extractorFactory;
        _ollamaService = ollamaService;
        _logger = logger;
    }

    public async Task<AIAnalysisResult> AnalyzeDocumentAsync(string filePath, string fileExtension)
    {
        var extractor = _extractorFactory.GetExtractor(fileExtension);

        if (extractor == null)
        {
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = $"نوع فایل '{fileExtension}' پشتیبانی نمی‌شود.",
                Summary = "پردازش نشد - نوع فایل پشتیبانی نمی‌شود",
                DocumentTypePrediction = "Unknown",
                Confidence = 0
            };
        }

        ExtractedDocumentText extractedText;
        try
        {
            extractedText = await extractor.ExtractAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در استخراج متن از فایل {FilePath}", filePath);
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = $"خطا در خواندن فایل: {ex.Message}",
                Summary = "پردازش ناموفق",
                DocumentTypePrediction = "Unknown",
                Confidence = 0
            };
        }

        if (!extractedText.HasContent)
        {
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = "هیچ متنی از فایل استخراج نشد (ممکن است کیفیت اسکن پایین باشد یا فایل خالی باشد).",
                Summary = "متنی برای تحلیل یافت نشد",
                DocumentTypePrediction = "Unknown",
                UsedOcr = extractedText.UsedOcr,
                Confidence = 0
            };
        }

        var prompt = EngineeringExtractionPromptBuilder.Build(extractedText.FullText);

        string rawModelOutput;
        try
        {
            rawModelOutput = await _ollamaService.GenerateAsync(prompt, new OllamaGenerateOptions
            {
                JsonMode = true,
                Temperature = 0.1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در فراخوانی Ollama برای فایل {FilePath}. آیا سرویس Ollama در حال اجراست؟", filePath);
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = $"خطا در ارتباط با موتور هوش مصنوعی محلی (Ollama): {ex.Message}",
                Summary = "پردازش ناموفق",
                DocumentTypePrediction = "Unknown",
                UsedOcr = extractedText.UsedOcr,
                Confidence = 0
            };
        }

        if (!AIJsonExtractor.TryParse<ExtractedEngineeringData>(rawModelOutput, out var extractedData) || extractedData == null)
        {
            _logger.LogWarning("خروجی مدل برای فایل {FilePath} قابل تبدیل به JSON نبود: {RawOutput}", filePath, rawModelOutput);
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = "خروجی هوش مصنوعی قابل تفسیر نبود. ممکن است لازم باشد از مدل بزرگ‌تری در Ollama استفاده کنید.",
                Summary = "پردازش ناموفق - خروجی نامعتبر",
                DocumentTypePrediction = "Unknown",
                UsedOcr = extractedText.UsedOcr,
                Confidence = 0
            };
        }

        var validTags = extractedData.EquipmentTags.Where(t => !string.IsNullOrWhiteSpace(t.TagNumber)).ToList();
        extractedData.EquipmentTags = validTags;

        var validSpecs = extractedData.Specifications.Where(s => !string.IsNullOrWhiteSpace(s.FieldName)).ToList();
        extractedData.Specifications = validSpecs;

        var keywords = new List<string>();
        if (extractedData.DocumentInfo?.Discipline != null) keywords.Add(extractedData.DocumentInfo.Discipline);
        if (extractedData.DocumentInfo?.DocumentType != null) keywords.Add(extractedData.DocumentInfo.DocumentType);
        keywords.AddRange(validTags.Select(t => t.TagNumber).Distinct());

        double confidence = 0.4;
        if (validTags.Count > 0) confidence += 0.2;
        if (validSpecs.Count > 0) confidence += 0.2;
        if (!extractedText.UsedOcr) confidence += 0.1;
        confidence = Math.Min(confidence, 0.9);

        return new AIAnalysisResult
        {
            Success = true,
            Summary = string.IsNullOrWhiteSpace(extractedData.DocumentInfo?.Title)
                ? $"مدرک شامل {validTags.Count} تگ تجهیز و {validSpecs.Count} مشخصه فنی استخراج‌شده"
                : extractedData.DocumentInfo!.Title!,
            Keywords = keywords,
            ExtractedTags = validTags.Select(t => t.TagNumber).ToList(),
            DocumentTypePrediction = extractedData.DocumentInfo?.DocumentType ?? "Unknown",
            Confidence = confidence,
            UsedOcr = extractedText.UsedOcr,
            ExtractedData = extractedData
        };
    }

    public async Task<AIAnalysisResult> AnalyzeTableDocumentAsync(string filePath, string fileExtension, string? documentHint = null)
    {
        var extractor = _extractorFactory.GetExtractor(fileExtension);

        if (extractor == null)
        {
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = $"نوع فایل '{fileExtension}' پشتیبانی نمی‌شود.",
                Summary = "پردازش نشد - نوع فایل پشتیبانی نمی‌شود",
                DocumentTypePrediction = "Unknown",
                Confidence = 0
            };
        }

        ExtractedDocumentText extractedText;
        try
        {
            extractedText = await extractor.ExtractAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در استخراج متن از فایل {FilePath}", filePath);
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = $"خطا در خواندن فایل: {ex.Message}",
                Summary = "پردازش ناموفق",
                DocumentTypePrediction = "Unknown",
                Confidence = 0
            };
        }

        if (!extractedText.HasContent)
        {
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = "هیچ متنی از فایل استخراج نشد (ممکن است کیفیت اسکن پایین باشد یا فایل خالی باشد).",
                Summary = "متنی برای تحلیل یافت نشد",
                DocumentTypePrediction = "Unknown",
                UsedOcr = extractedText.UsedOcr,
                Confidence = 0
            };
        }

        // سند روی مرز خط به چند تکه تقسیم می‌شود تا جداول بزرگ (صدها ردیف) وسط یک ردیف بریده نشوند؛
        // هر تکه جدا به مدل داده می‌شود و نتایج/هشدارها در پایان ادغام می‌شوند
        var chunks = EngineeringExtractionPromptBuilder.SplitTableTextIntoChunks(extractedText.FullText);

        ExtractedTableDocumentInfo? mergedDocumentInfo = null;
        var mergedRows = new List<ExtractedTableRow>();
        var mergedWarnings = new List<string>();
        var parsedAnyChunk = false;

        for (var i = 0; i < chunks.Count; i++)
        {
            var prompt = EngineeringExtractionPromptBuilder.BuildForTableDocument(chunks[i], documentHint);

            string rawModelOutput;
            try
            {
                rawModelOutput = await _ollamaService.GenerateAsync(prompt, new OllamaGenerateOptions
                {
                    JsonMode = true,
                    Temperature = 0.1
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در فراخوانی Ollama برای چانک {ChunkIndex} از فایل {FilePath}. آیا سرویس Ollama در حال اجراست؟", i + 1, filePath);
                mergedWarnings.Add($"چانک {i + 1} از {chunks.Count}: خطا در ارتباط با موتور هوش مصنوعی محلی.");
                continue;
            }

            if (!AIJsonExtractor.TryParse<ExtractedTableData>(rawModelOutput, out var chunkData) || chunkData == null)
            {
                _logger.LogWarning("خروجی مدل برای چانک {ChunkIndex} از فایل {FilePath} قابل تبدیل به JSON نبود: {RawOutput}", i + 1, filePath, rawModelOutput);
                mergedWarnings.Add($"چانک {i + 1} از {chunks.Count}: خروجی هوش مصنوعی قابل تفسیر نبود.");
                continue;
            }

            parsedAnyChunk = true;
            mergedDocumentInfo ??= chunkData.DocumentInfo;
            mergedRows.AddRange(chunkData.Rows.Where(HasAnyMeaningfulValue));
            mergedWarnings.AddRange(chunkData.Warnings);
        }

        if (!parsedAnyChunk)
        {
            return new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = "خروجی هوش مصنوعی برای هیچ‌یک از بخش‌های سند قابل تفسیر نبود. ممکن است لازم باشد از مدل بزرگ‌تری در Ollama استفاده کنید.",
                Summary = "پردازش ناموفق - خروجی نامعتبر",
                DocumentTypePrediction = "Unknown",
                UsedOcr = extractedText.UsedOcr,
                Warnings = mergedWarnings,
                Confidence = 0
            };
        }

        var summary = !string.IsNullOrWhiteSpace(mergedDocumentInfo?.Title)
            ? $"{mergedDocumentInfo!.Title} - {mergedRows.Count} ردیف استخراج‌شده"
            : $"{mergedRows.Count} ردیف از {chunks.Count} بخش سند استخراج شد";

        var tableData = new ExtractedTableData
        {
            DocumentInfo = mergedDocumentInfo,
            Rows = mergedRows,
            RowCount = mergedRows.Count,
            Warnings = mergedWarnings,
            Summary = summary
        };

        var extractedTagNumbers = mergedRows
            .Where(r => !string.IsNullOrWhiteSpace(r.TagNumber))
            .Select(r => r.TagNumber!)
            .Distinct()
            .ToList();

        var keywords = new List<string>();
        if (mergedDocumentInfo?.Discipline != null) keywords.Add(mergedDocumentInfo.Discipline);
        if (mergedDocumentInfo?.DocumentType != null) keywords.Add(mergedDocumentInfo.DocumentType);
        keywords.AddRange(extractedTagNumbers);

        double confidence = 0.4;
        if (mergedRows.Count > 0) confidence += 0.3;
        if (!extractedText.UsedOcr) confidence += 0.1;
        if (mergedWarnings.Count == 0) confidence += 0.1;
        confidence = Math.Min(confidence, 0.9);

        return new AIAnalysisResult
        {
            Success = true,
            Summary = summary,
            Keywords = keywords,
            ExtractedTags = extractedTagNumbers,
            DocumentTypePrediction = mergedDocumentInfo?.DocumentType ?? "Unknown",
            Confidence = confidence,
            UsedOcr = extractedText.UsedOcr,
            Warnings = mergedWarnings,
            ExtractedTableData = tableData
        };
    }

    private static bool HasAnyMeaningfulValue(ExtractedTableRow row)
    {
        return !string.IsNullOrWhiteSpace(row.TagNumber)
            || !string.IsNullOrWhiteSpace(row.Description)
            || !string.IsNullOrWhiteSpace(row.DcsTagNo)
            || !string.IsNullOrWhiteSpace(row.LoopNo)
            || !string.IsNullOrWhiteSpace(row.CableNo)
            || !string.IsNullOrWhiteSpace(row.Remarks);
    }
}
