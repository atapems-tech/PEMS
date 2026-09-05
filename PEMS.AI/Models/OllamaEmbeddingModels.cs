namespace PEMS.AI.Models;

public class OllamaEmbeddingRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public class OllamaEmbeddingResponse
{
    public float[]? Embedding { get; set; }
}
