using PEMS.Application.AI.OCR;

namespace PEMS.Application.AI.Parser;

public class ImageTextExtractor : ITextExtractor
{
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

    private readonly IOcrService _ocrService;

    public ImageTextExtractor(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public bool CanHandle(string fileExtension) =>
        SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);

    public async Task<ExtractedDocumentText> ExtractAsync(string filePath)
    {
        var text = await _ocrService.RecognizeFileAsync(filePath);
        return new ExtractedDocumentText { FullText = text, UsedOcr = true, PageCount = 1 };
    }
}
