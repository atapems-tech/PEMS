namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش AD: این‌جا واقعیتِ دامنه‌ی
/// تولید است (چه چیزی مصرف شد) - جدا از InventoryTransaction (رویداد دامنه‌ی موجودی). این دو
/// مسئولیت را در یک جدول ادغام نمی‌کنیم. Append-only مثل InventoryTransaction (کلاس ساده، بدون
/// BaseEntity/IsActive) - هیچ مسیر ویرایش/حذفی برای این Entity در سرویس‌ها وجود ندارد.
///
/// InventoryTransactionId فقط وقتی پر می‌شود که کاربر صریحاً اکشن جداگانه‌ی «ثبت در موجودی» را
/// اجرا کند (طبق تصمیم معماری AD-Phase1-01: ثبت عملیاتی تولید و ارسال به موجودی دو گام مجزا و
/// عمدی‌اند - طبق دستور صریح «Do not automatically post inventory transactions»). تا وقتی Null
/// است یعنی این مصرف هنوز به موجودی منعکس نشده.
/// </summary>
public class ProductionInput
{
    public int ProductionInputId { get; set; }

    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal Quantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    // قلاب ژنالوژی (فاز ۱ فقط رابطه؛ بدون موتور ژنالوژی کامل) - اگر ماده‌ی مصرفی خودش خروجیِ
    // یک بچ تولید قبلی بوده (مثلاً محصول میانی)
    public int? SourceProductionBatchId { get; set; }
    public ProductionBatch? SourceProductionBatch { get; set; }

    public int? InventoryTransactionId { get; set; }
    public InventoryTransaction? InventoryTransaction { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public string? Notes { get; set; }
}
