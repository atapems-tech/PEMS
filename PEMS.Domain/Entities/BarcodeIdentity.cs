namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System»: یک لایه‌ی شناسایی مستقل و اشتراکی برای کل PEMS -
/// نه یک موجودیت موازی برای Equipment/Material/Warehouse. بارکد فقط یک توکن پایدار حمل می‌کند
/// (TargetType+TargetId) که به یک رکورد واقعیِ از قبل موجود در PEMS Resolve می‌شود؛ خودِ داده‌ی
/// کسب‌وکاری هرگز داخل بارکد کدگذاری نمی‌شود (طبق بند «Barcode Identity Model»).
///
/// طبق بند «Stability»: Code به هیچ فیلد تغییرپذیری (Tag، نام، Functional Location، Bin و...)
/// وابسته نیست - فقط به TargetType+TargetId. تغییر آن فیلدها هرگز بارکد را باطل نمی‌کند.
/// </summary>
public class BarcodeIdentity
{
    public int BarcodeIdentityId { get; set; }

    // توکن پایدار و غیرقابل‌حدس - طبق بند «Barcode Identity Model»: هرگز مشتق از TargetId خام
    // نیست (که قابل شمارش/حدس می‌بود) - یک رشته‌ی تصادفی رمزنگاری‌شده با پیشوند PEMS-BR-.
    public string Code { get; set; } = string.Empty;

    // Code128, QR - طبق بند «Barcode Types» ممیزی؛ Code39/GS1 عمداً پیاده‌سازی نشدند (سند معماری)
    public string Symbology { get; set; } = "Code128";

    // Equipment, MaterialItem, Warehouse, StorageLocation - طبق ممیزی Source-Level (بخش ۳)،
    // فقط موجودیت‌هایی که واقعاً در PEMS وجود دارند - رشته (نه Enum) طبق قرارداد موجود پروژه
    // (StrategyType/WorkOrderType/...و غیره همگی رشته‌اند، نه Enum جدید).
    public string TargetType { get; set; } = string.Empty;

    public int TargetId { get; set; }

    // Active, Disabled, Revoked, Replaced - طبق بند «Barcode Lifecycle»
    public string Status { get; set; } = "Active";

    public int? CreatedById { get; set; }
    public Employee? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public int PrintCount { get; set; }
    public DateTime? LastPrintedDate { get; set; }

    public DateTime? DisabledDate { get; set; }
    public int? DisabledById { get; set; }
    public Employee? DisabledBy { get; set; }
    public string? DisableReason { get; set; }

    // طبق بند «Do not silently delete barcode history»: Revoke هرگز رکورد را حذف نمی‌کند -
    // فقط Status را تغییر می‌دهد؛ تاریخچه برای Audit همیشه باقی می‌ماند.
    public DateTime? RevokedDate { get; set; }
    public int? RevokedById { get; set; }
    public Employee? RevokedBy { get; set; }
    public string? RevokeReason { get; set; }

    // طبق بند «Replace»: بارکد قدیمی → Revoked/Replaced، بارکد جدید → همان رکورد کسب‌وکاری.
    // هر دو جهت این زنجیره برای بازسازی آسان تاریخچه نگه داشته می‌شوند.
    public int? ReplacedByBarcodeIdentityId { get; set; }
    public BarcodeIdentity? ReplacedByBarcodeIdentity { get; set; }

    public int? ReplacesBarcodeIdentityId { get; set; }
    public BarcodeIdentity? ReplacesBarcodeIdentity { get; set; }
}
