using System.Text;
using Microsoft.Extensions.Logging;
using PEMS.Application.AI.OCR;
using UglyToad.PdfPig;

namespace PEMS.Application.AI.Parser;

public class PdfTextExtractor : ITextExtractor
{
    private const int MinTextLengthPerPage = 20;

    private readonly IOcrService _ocrService;
    private readonly ILogger<PdfTextExtractor> _logger;

    public PdfTextExtractor(IOcrService ocrService, ILogger<PdfTextExtractor> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    public bool CanHandle(string fileExtension) =>
        string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractedDocumentText> ExtractAsync(string filePath)
    {
        var sb = new StringBuilder();
        bool usedOcr = false;
        int pageCount = 0;

        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                pageCount++;
                var pageText = page.Text ?? string.Empty;

                if (pageText.Trim().Length >= MinTextLengthPerPage)
                {
                    sb.AppendLine(pageText);
                    continue;
                }

                foreach (var image in page.GetImages())
                {
                    byte[]? imageBytes = null;

                    if (image.TryGetPng(out var pngBytes))
                        imageBytes = pngBytes;
                    else if (image.RawBytes.Length > 0)
                        imageBytes = image.RawBytes.ToArray();

                    if (imageBytes == null || imageBytes.Length == 0)
                        continue;

                    string ocrText;
                    try
                    {
                        ocrText = await _ocrService.RecognizeAsync(imageBytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "OCR روی تصویر صفحه {PageNumber} از فایل {FilePath} ناموفق بود", page.Number, filePath);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        usedOcr = true;
                        sb.AppendLine(ocrText);
                    }
                }
            }
        }

        return new ExtractedDocumentText { FullText = sb.ToString(), UsedOcr = usedOcr, PageCount = pageCount };
    }
}
