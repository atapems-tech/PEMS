using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// طبق «PEMS Phase 2A — Global Search Security»: LetterSearchProvider/
/// EngineeringDocumentSearchProvider هر دو به IKnowledgeBaseService (جستجوی معنایی Ollama)
/// وابسته‌اند، اما تست‌های امنیتی این فاز باید کاملاً قطعی/بدون وابستگی خارجی بمانند - این
/// پیاده‌سازی همیشه نتیجه‌ی خالی برمی‌گرداند (دقیقاً همون رفتاری که Provider ها وقتی Ollama در
/// دسترس نیست از آن پشتیبانی می‌کنند - try/catch موجود، نه رفتار جدید).
/// </summary>
public class FakeKnowledgeBaseService : IKnowledgeBaseService
{
    public Task IndexAsync(string sourceType, int sourceId, string content) => Task.CompletedTask;

    public Task<List<KnowledgeBaseSearchResultDto>> SearchAsync(string query, int topK = 5) =>
        Task.FromResult(new List<KnowledgeBaseSearchResultDto>());

    public Task<int> GetEntryCountAsync() => Task.FromResult(0);
}
