namespace PEMS.Application.AI.Parser;

public class ExtractedDocumentText
{
    public string FullText { get; set; } = string.Empty;
    public bool UsedOcr { get; set; }
    public int PageCount { get; set; }
    public bool HasContent => !string.IsNullOrWhiteSpace(FullText);
}
