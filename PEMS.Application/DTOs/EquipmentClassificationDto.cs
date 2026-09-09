namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۱۴/۲۵: Dashboard کاملاً
/// SQL-Aggregated است (GROUP BY در دیتابیس) - هرگز ۲۰٬۰۰۰+ رکورد Equipment را به حافظه بارگذاری
/// نمی‌کند. جایگزین GetClassificationReportAsync فاز ۲ شد که کل جدول Unclassified را
/// ToListAsync می‌کرد (نقض همین اصل عملکردی که فاز ۳ صریحاً آن را ممنوع کرده است).
/// </summary>
public class ClassificationDashboardDto
{
    public int TotalActiveEquipment { get; set; }
    public int ClassifiedCount { get; set; }
    public int UnclassifiedCount { get; set; }
    public double CoveragePercent { get; set; }
    public int PendingReviewCount { get; set; }

    public List<CountByKeyDto> ByConfidence { get; set; } = new();
    public List<CountByKeyDto> BySource { get; set; } = new();
    public List<CountByKeyDto> ByEquipmentClass { get; set; } = new();
    public List<CountByKeyDto> PendingSuggestionsByConfidence { get; set; } = new();
    public List<CountByKeyDto> UnclassifiedByDiscipline { get; set; } = new();
    public List<CountByKeyDto> TopUnclassifiedEquipmentTypes { get; set; } = new();
    public List<CountByKeyDto> TopUnclassifiedEquipmentNames { get; set; } = new();
    public List<CountByKeyDto> TopUnclassifiedTagPatterns { get; set; } = new();
}

public class CountByKeyDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// طبق بند ۹/۱۵: یک ردیف صف بازبینی - Reviewer باید بفهمد چرا این پیشنهاد مطرح شده، بدون نیاز
/// به مراجعه به EF Entity یا کد سرویس.
/// </summary>
public class ReviewQueueItemDto
{
    public int SuggestionId { get; set; }
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public string? DisciplineName { get; set; }
    public int SuggestedEquipmentClassId { get; set; }
    public string SuggestedEquipmentClassName { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ReviewQueuePageDto
{
    public List<ReviewQueueItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// طبق بند ۱۲: نتیجه‌ی یک اجرای Backfill - چند رکورد به‌صورت خودکار (EXACT/HIGH) طبقه‌بندی شدند
/// در برابر چند Suggestion جدید (MEDIUM/LOW) برای بازبینی ساخته شد؛ این دو عدد هرگز نباید با هم
/// مخلوط شوند چون معنای اعتمادپذیری‌شان کاملاً متفاوت است.
/// </summary>
public class BackfillResultDto
{
    public int AutoClassifiedCount { get; set; }
    public int SuggestionsCreatedCount { get; set; }
}
