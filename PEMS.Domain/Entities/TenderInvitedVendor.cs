using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// فروشنده‌ی دعوت‌شده به یک Tender - QualificationConfirmed یک عکس‌فوری (snapshot) از وضعیت
/// صلاحیت فروشنده در لحظه‌ی دعوت است (نه یک ارجاع زنده)، تا تغییر بعدی وضعیت Vendor سابقه‌ی
/// تاریخی را تحریف نکند.
/// </summary>
public class TenderInvitedVendor : BaseEntity
{
    [Key]
    public int TenderInvitedVendorId { get; set; }

    public int TenderId { get; set; }
    public Tender? Tender { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public DateTime InvitedDate { get; set; } = DateTime.Now;

    public bool QualificationConfirmed { get; set; }

    public bool IsActive { get; set; } = true;
}
