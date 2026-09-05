namespace PEMS.Application.AI.Parser;

public interface IDocumentTextExtractorFactory
{
    ITextExtractor? GetExtractor(string fileExtension);
}

public class DocumentTextExtractorFactory : IDocumentTextExtractorFactory
{
    private readonly IEnumerable<ITextExtractor> _extractors;

    public DocumentTextExtractorFactory(IEnumerable<ITextExtractor> extractors)
    {
        _extractors = extractors;
    }

    public ITextExtractor? GetExtractor(string fileExtension)
    {
        return _extractors.FirstOrDefault(x => x.CanHandle(fileExtension));
    }
}
