using PEMS.Application.AI.Search.Models;

namespace PEMS.Application.AI.Search.Interfaces;

public interface IDocumentSearchAIService
{
    Task<List<DocumentSearchResult>> SearchAsync(
        DocumentSearchRequest request);
}