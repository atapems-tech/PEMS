using DocumentFormat.OpenXml.Packaging;

namespace PEMS.Application.AI.Parser;

public class WordTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) =>
        string.Equals(fileExtension, ".docx", StringComparison.OrdinalIgnoreCase);

    public Task<ExtractedDocumentText> ExtractAsync(string filePath)
    {
        using var wordDocument = WordprocessingDocument.Open(filePath, false);
        var body = wordDocument.MainDocumentPart?.Document?.Body;
        var text = body?.InnerText ?? string.Empty;

        var result = new ExtractedDocumentText { FullText = text, UsedOcr = false, PageCount = 1 };
        return Task.FromResult(result);
    }
}
