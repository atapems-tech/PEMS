using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// تاریخچه‌ی append-only تغییرات ارزش/دامنه‌ی قرارداد (Variation/Change Order/Amendment) - طبق
/// ممیزی Package Procurement Contract Finalization (§E، قاعده‌ی کسب‌وکار ۱۵). الگوی این Entity
/// دقیقاً از DocumentRevision گرفته شده - هیچ Entity تاریخچه‌ی جدیدی اختراع نشده.
/// ارزش اصلی قرارداد (Contract.ContractValue) هرگز به‌صورت مستقیم بازنویسی نمی‌شود؛ تغییرات فقط
/// از طریق یک ردیف تایید‌شده در همین جدول روی Contract.CurrentValue اعمال می‌شوند.
/// </summary>
public class ContractRevision : BaseEntity
{
    [Key]
    public int ContractRevisionId { get; set; }

    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    public int RevisionNo { get; set; }

    // Variation, ChangeOrder, Amendment, Correction
    public string ChangeType { get; set; } = "Variation";

    public decimal? PreviousValue { get; set; }

    public decimal? NewValue { get; set; }

    public string? Reason { get; set; }

    public string? ChangeDescription { get; set; }

    public int? RequestedById { get; set; }
    public Employee? RequestedBy { get; set; }

    public DateTime RequestedDate { get; set; } = DateTime.Now;

    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    // Pending, Approved, Rejected
    public string Status { get; set; } = "Pending";

    public bool IsActive { get; set; } = true;
}
