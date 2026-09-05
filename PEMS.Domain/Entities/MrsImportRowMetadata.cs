namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند «Material Master Import — Excel Metadata Extraction»: هر ستون واقعی فایل Excel
/// (شناخته‌شده یا ناشناخته) یک ردیف مستقل این‌جا می‌گیرد - نه فقط ستون‌هایی که به یک فیلد
/// Typed روی MrsImportRow نگاشت شدند. OriginalHeader همیشه دقیقاً همان متن ستون Excel است
/// (حتی اگر غلط‌املایی داشته باشد مثل "Wight" یا فارسی باشد مثل "چک")؛ CanonicalField فقط
/// وقتی پر می‌شود که یک نگاشت معنایی شناخته‌شده برایش وجود داشته باشد - در غیر این صورت
/// null می‌ماند (نه حذف می‌شود، نه حدس زده می‌شود).
/// </summary>
public class MrsImportRowMetadata
{
    public int MrsImportRowMetadataId { get; set; }

    public int MrsImportRowId { get; set; }
    public MrsImportRow? Row { get; set; }

    // موقعیت واقعی ستون در شیت - برای ردیابی کامل تا فایل/شیت/ستون مبدأ
    public int ColumnIndex { get; set; }

    // متن دقیق سرستون همان‌طور که در فایل اکسل بود - دست‌نخورده
    public string OriginalHeader { get; set; } = string.Empty;

    // مثلاً VendorName, PurchaseOrderNumber, Weight, ... - null یعنی ستون ناشناخته/سفارشی
    public string? CanonicalField { get; set; }

    public string? Value { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
