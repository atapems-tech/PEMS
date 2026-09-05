using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class Vendor : BaseEntity
{
    [Key]
    public int VendorId { get; set; }

    public string Name { get; set; } = string.Empty;

    // طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۴.۳.۱: کد فروشنده‌ی تعیین‌شده توسط
    // CONTRACTOR (نمونه سند: "FSK") که در شماره‌ی رسمی مدرک (J-85-XXX-ME-DSH-...) به‌عنوان XXX
    // استفاده می‌شود. سند الگوریتمی برای تولید این کد نمی‌دهد - دستی/کارفرما تعیین می‌شود.
    public string? VendorCode { get; set; }

    public string? Country { get; set; }

    public string? ContactName { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    // Low / Medium / High - آخرین نتیجه‌ی محاسبه‌ی ریسک AI (کش شده، برای نمایش سریع در لیست‌ها)
    public string RiskLevel { get; set; } = "Unknown";

    public double? RiskScore { get; set; }

    public DateTime? RiskAssessedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public ICollection<VDRItem> VDRItems { get; set; } = new List<VDRItem>();

    // طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability) - سمت معکوس EngineeringDocument.VendorId
    public ICollection<EngineeringDocument> EngineeringDocuments { get; set; } = new List<EngineeringDocument>();
}
