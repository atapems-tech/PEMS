using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.AI.Interfaces;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی Semantic Search: Embedding محلی از طریق Ollama تولید می‌شود، در SQL Server
/// (به‌صورت JSON) ذخیره می‌شود، و جستجو با شباهت کسینوسی در حافظه محاسبه می‌شود - بدون نیاز به
/// هیچ زیرساخت Vector Database جداگانه.
/// </summary>
public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly PEMSDbContext _context;
    private readonly IOllamaService _ollamaService;

    public KnowledgeBaseService(PEMSDbContext context, IOllamaService ollamaService)
    {
        _context = context;
        _ollamaService = ollamaService;
    }

    public async Task IndexAsync(string sourceType, int sourceId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        float[] embedding;

        try
        {
            embedding = await _ollamaService.GenerateEmbeddingAsync(content);
        }
        catch
        {
            // اگر مدل Embedding نصب نباشد یا Ollama در دسترس نباشد، فقط از Index کردن این مورد
            // صرف‌نظر می‌کنیم - نباید کل عملیات اصلی (مثلاً Routing یک مدرک) را متوقف کند
            return;
        }

        if (embedding.Length == 0)
            return;

        var existing = await _context.KnowledgeBaseEntries
            .FirstOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == sourceId);

        var embeddingJson = JsonSerializer.Serialize(embedding);

        if (existing != null)
        {
            existing.Content = content;
            existing.EmbeddingJson = embeddingJson;
            existing.UpdatedDate = DateTime.Now;
        }
        else
        {
            await _context.KnowledgeBaseEntries.AddAsync(new KnowledgeBaseEntry
            {
                SourceType = sourceType,
                SourceId = sourceId,
                Content = content,
                EmbeddingJson = embeddingJson,
                CreatedDate = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<KnowledgeBaseSearchResultDto>> SearchAsync(string query, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<KnowledgeBaseSearchResultDto>();

        float[] queryEmbedding;

        try
        {
            queryEmbedding = await _ollamaService.GenerateEmbeddingAsync(query);
        }
        catch
        {
            return new List<KnowledgeBaseSearchResultDto>();
        }

        if (queryEmbedding.Length == 0)
            return new List<KnowledgeBaseSearchResultDto>();

        var entries = await _context.KnowledgeBaseEntries.ToListAsync();

        var scored = new List<KnowledgeBaseSearchResultDto>();

        foreach (var entry in entries)
        {
            float[]? entryEmbedding;

            try
            {
                entryEmbedding = JsonSerializer.Deserialize<float[]>(entry.EmbeddingJson);
            }
            catch
            {
                continue;
            }

            if (entryEmbedding == null || entryEmbedding.Length != queryEmbedding.Length)
                continue;

            var similarity = CosineSimilarity(queryEmbedding, entryEmbedding);

            scored.Add(new KnowledgeBaseSearchResultDto
            {
                SourceType = entry.SourceType,
                SourceId = entry.SourceId,
                Content = entry.Content,
                SimilarityScore = similarity
            });
        }

        return scored
            .OrderByDescending(x => x.SimilarityScore)
            .Take(topK)
            .ToList();
    }

    public async Task<int> GetEntryCountAsync()
    {
        return await _context.KnowledgeBaseEntries.CountAsync();
    }


    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
