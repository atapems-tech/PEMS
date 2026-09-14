namespace PEMS.Web.Models;

// طبق «PEMS Phase 2C-4B — Safe VDR Resource Header Pilot»: این مدل صرفاً بخش نمایشی هدر
// صفحه‌ی Details یک منبع را نمایندگی می‌کند - هیچ منطق مجوز/گردش‌کار/کسب‌وکاری این‌جا نیست.
// تصمیم این‌که StatusText/StatusTone چه باشد، همیشه در همان View مصرف‌کننده (بر اساس داده‌ی
// از‌قبل‌موجود روی DTO خودش) محاسبه می‌شود - دقیقاً همان تقسیم مسئولیتی که
// StageDiagramViewModel (فاز ۲C-3) قبلاً برقرار کرد.
public enum ResourceHeaderTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

public class ResourceHeaderBadge
{
    public string Text { get; set; } = "";
    public ResourceHeaderTone Tone { get; set; } = ResourceHeaderTone.Neutral;
}

public class ResourceHeaderViewModel
{
    public string Identifier { get; set; } = "";

    // طبق ممیزی فاز ۲C-4A: در VDR این یک متن ساده‌ی کنار شناسه است (مثل "Rev 3")، نه یک
    // Badge - عمداً Nullable و بدون کلاس badge رندر می‌شود تا دقیقاً همان ظاهر امروز حفظ شود.
    public string? SecondaryText { get; set; }

    public string MetaLine { get; set; } = "";

    public string StatusText { get; set; } = "";
    public ResourceHeaderTone StatusTone { get; set; }

    // Badgeهای شرطی سمت راست، بعد از Status (مثل "Overdue") - در VDR امروز حداکثر یکی است،
    // ولی به‌صورت لیست تعریف شده تا نیازی به فیلد اختصاصی جدید برای هر Badge بعدی نباشد.
    public List<ResourceHeaderBadge> ConditionalBadges { get; set; } = new();
}
