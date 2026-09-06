using PEMS.Application.AI.Search.Interfaces;
using PEMS.Application.AI.Search.Models;
using PEMS.Application.Interfaces;

namespace PEMS.Application.AI.Search.Services;

/// <summary>
/// جستجوی مدارک مهندسی؛ عنوان/شماره مدرک/توضیحات/نوع مدرک را با فیلترهای اختیاری
/// (دیسیپلین، تگ تجهیز، نوع مدرک، وضعیت) ترکیب می‌کند و بر اساس محل و نوع تطابق امتیازدهی می‌کند.
/// تگ تجهیز به‌صورت جستجوی متنی روی عنوان/توضیحات مدرک اعمال می‌شود (چون ارتباط مستقیم
/// مدرک ↔ تگ در این نسخه هنوز وجود ندارد).
/// </summary>
public class DocumentSearchAIService : IDocumentSearchAIService
{
    private readonly IEngineeringDocumentRepository _repository;

    public DocumentSearchAIService(IEngineeringDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<DocumentSearchResult>> SearchAsync(DocumentSearchRequest request)
    {
        // طبق «PEMS — Global Grid Hardening» P1: قبلاً همیشه کل جدول فعال EngineeringDocuments
        // (با ۵ Include) بارگذاری می‌شد، حتی وقتی Discipline/DocumentType/Status انتخاب شده
        // بود. حالا این سه فیلتر در SQL اعمال می‌شوند - فقط EquipmentTag/عبارت آزاد (که روی
        // Title/Description/Attachments وزن‌دار امتیازدهی می‌شوند) در حافظه باقی می‌مانند.
        var documents = await _repository.GetActiveForSearchAsync(request.Discipline, request.DocumentType, request.Status);

        var query = documents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.EquipmentTag))
            query = query.Where(x =>
                (x.Title?.Contains(request.EquipmentTag, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Description?.Contains(request.EquipmentTag, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Attachments.Any(a => a.AITags != null &&
                    a.AITags.Contains(request.EquipmentTag, StringComparison.OrdinalIgnoreCase)));

        var results = new List<DocumentSearchResult>();

        var term = request.Direction?.Trim() ?? string.Empty;

        foreach (var doc in query)
        {
            double score = 0;
            var reasons = new List<string>();

            if (!string.IsNullOrWhiteSpace(term))
            {
                if (doc.DocumentNo.Equals(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.0;
                    reasons.Add("exact document number match");
                }
                else if (doc.DocumentNo.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.7;
                    reasons.Add("document number contains query");
                }

                if (doc.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.6;
                    reasons.Add("title match");
                }

                if (doc.Description != null && doc.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.3;
                    reasons.Add("description match");
                }

                foreach (var attachment in doc.Attachments)
                {
                    if (attachment.AISummary != null && attachment.AISummary.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.5;
                        reasons.Add("AI summary match");
                    }

                    if (attachment.AIKeywords != null && attachment.AIKeywords.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.4;
                        reasons.Add("AI keyword match");
                    }

                    if (attachment.AITags != null && attachment.AITags.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.5;
                        reasons.Add("AI-extracted tag match");
                    }
                }

                if (score == 0)
                    continue;
            }
            else
            {
                score = 0.2;
                reasons.Add("matched by filters");
            }

            results.Add(new DocumentSearchResult
            {
                DocumentId = doc.DocumentId,
                DocumentNumber = doc.DocumentNo,
                Title = doc.Title,
                Reason = reasons.Count > 0 ? string.Join(", ", reasons.Distinct()) : "Matched by document information",
                SimilarityScore = Math.Min(score, 1.0)
            });
        }

        return results
            .OrderByDescending(x => x.SimilarityScore)
            .Take(50)
            .ToList();
    }
}
