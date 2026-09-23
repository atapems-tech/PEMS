using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// تاریخچه‌ی append-only تغییرات وضعیت صلاحیت‌سنجی فروشنده - طبق ممیزی Package Procurement
/// Contract Finalization (§E). فیلدهای Qualification روی Vendor فقط Cache آخرین ردیف این
/// تاریخچه‌اند - دقیقاً همان الگوی Document/DocumentRevision (Document فیلدهای Cache دارد،
/// DocumentRevision مرجع حقیقت است).
/// </summary>
public class VendorQualificationHistory : BaseEntity
{
    [Key]
    public int VendorQualificationHistoryId { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string? PreviousStatus { get; set; }

    // NotQualified, Pending, Qualified, Suspended, Expired
    public string NewStatus { get; set; } = string.Empty;

    public string? Category { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? EvidenceReference { get; set; }

    public int? ChangedById { get; set; }
    public Employee? ChangedBy { get; set; }

    public DateTime ChangeDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }

    public bool IsActive { get; set; } = true;
}
