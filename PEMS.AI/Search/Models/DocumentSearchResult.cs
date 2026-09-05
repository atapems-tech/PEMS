namespace PEMS.Application.AI.Search.Models;

public class DocumentSearchResult
{
    public int DocumentId { get; set; }


    public string DocumentNumber { get; set; } = string.Empty;


    public string Title { get; set; } = string.Empty;


    public string Reason { get; set; } = string.Empty;


    public double SimilarityScore { get; set; }
}