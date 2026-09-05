namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» Appendix 3: کدهای تایپولوژی مدرک فروشنده
/// (۷۹ کد دو-حرفی، مستقل از Discipline و DocumentTypeCode) که در شماره‌ی رسمی SUB-CONTRACTOR/
/// Vendor (3863-XX-VD-TT-UU-MMMM-NNNNNN-YYY) به‌عنوان TT استفاده می‌شود.
/// </summary>
public class VendorDocumentTypologyCode
{
    public int VendorDocumentTypologyCodeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // عنوان دسته‌بندی سند (مثلاً "INSTRUMENTATION / AUTOMATION & PROCESS CONTROL DESIGN")
    public string Category { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }
}
