using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-04: جایگزین همه‌ی الگوهای ناامن Count()+1 پراکنده
/// در کنترلرها (که باعث سه پیاده‌سازی مستقل و ناهماهنگ WorkOrderNumber شده بود). هر ردیف یک
/// شمارنده مجزا برای یک (DocumentType, ScopeKey) است - مثلاً (WorkOrder, "2026") یا
/// (MaintenancePlan, "80-E-5502"). افزایش مقدار توسط NumberingService با یک UPDATE اتمیک انجام
/// می‌شود، نه با خواندن Count() و نوشتن دوباره.
/// </summary>
public class NumberingSequence : BaseEntity
{
    public int NumberingSequenceId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string ScopeKey { get; set; } = string.Empty;

    public int CurrentValue { get; set; }

    public string Prefix { get; set; } = string.Empty;

    public int PadWidth { get; set; } = 4;

    public bool IncludeScopeInFormat { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
