using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public static class PurchaseOrderStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

public class PurchaseOrder : BaseEntity
{
    [Key]
    public int PoId { get; set; }

    public string PoNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    // در صورت وجود قرارداد رسمی (اختیاری - در بسیاری از PO های واقعی هنوز ثبت نشده)
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    public string? Description { get; set; }

    public decimal? Value { get; set; }

    public string? Currency { get; set; } = "USD";

    // Draft, Issued, Closed, Cancelled
    // پیش‌فرض Issued برای سازگاری با داده‌ی قدیمی و مسیرهای Import حفظ شده؛ PO های دستی جدید
    // توسط PurchaseOrderService.CreateAsync با Draft ساخته می‌شوند و فقط با IssueAsync (مجوز
    // Approve + SoD) به Issued می‌روند.
    public string Status { get; set; } = PurchaseOrderStatuses.Issued;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<VDRItem> VDRItems { get; set; } = new List<VDRItem>();
}
