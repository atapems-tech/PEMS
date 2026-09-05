namespace PEMS.Application.DTOs;

public class KnowledgeBaseSearchResultDto
{
    public string SourceType { get; set; } = "";
    public int SourceId { get; set; }
    public string Content { get; set; } = "";
    public double SimilarityScore { get; set; }
}
