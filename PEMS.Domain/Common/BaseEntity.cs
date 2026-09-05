using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Common;

/// <summary>
/// طبق PEMS Architecture &amp; Development Bible (Volume V - Database Design):
/// فیلدهای مشترک واقعاً جدید که تا الان نداشتیم - CreatedBy/ModifiedBy (چه کسی، نه فقط چه زمانی)،
/// RowVersion (Concurrency واقعی، جلوگیری از رونویسی هم‌زمان دو کاربر روی یک رکورد)، و TenantId
/// (آماده‌سازی برای چندشرکتی/چندسازمانی آینده - فعلاً استفاده نمی‌شود، Null می‌ماند).
///
/// عمداً شامل CreatedDate/ModifiedDate/IsActive نشده: این فیلدها از قبل با نام‌ها و معناهای
/// کمی متفاوت (IsActive vs. Archive, نه Soft-Delete) در ده‌ها Entity/Query موجود پخش شده‌اند؛
/// یکپارچه‌سازی زودهنگام آن‌ها ریسک شکستن کد فعلی را داشت بدون سود عملی فوری.
/// نسخه‌ی اول این استاندارد روی هسته‌ی مرکزی (Document, Vendor, Contract, ProcurementPackage,
/// PurchaseOrder, MaterialRequisition, Equipment) اعمال شده؛ باقی Entity های قدیمی‌تر (که هنوز UI
/// ندارند) در فاز بعدی که واقعاً روی آن‌ها کار می‌شود به این استاندارد مهاجرت می‌کنند.
/// </summary>
public abstract class BaseEntity
{
    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public int? TenantId { get; set; }
}
