using PEMS.AI.Models;

namespace PEMS.AI.Interfaces;

public interface IOllamaService
{
    Task<string> GenerateAsync(string prompt);
    Task<string> GenerateAsync(string prompt, OllamaGenerateOptions options);

    // طبق AI Architecture (Volume VIII - Bible): تولید Embedding محلی برای Semantic Search،
    // بدون نیاز به Vector Database جداگانه یا سرویس ابری
    Task<float[]> GenerateEmbeddingAsync(string text);
}
