namespace PEMS.Application.AI.Parser;

public interface ITextExtractor
{
    bool CanHandle(string fileExtension);
    Task<ExtractedDocumentText> ExtractAsync(string filePath);
}
