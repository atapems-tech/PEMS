namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» Appendix 2: ۲۹ کد رسمی نوع مدرک
/// (MRQ, SPC, POR, DWG, DSH, RPT, ...). قبلاً DocumentNumberingService کد نوع را با برداشتن
/// «۳ حرف اول رشتهٔ آزاد Document.DocumentType» می‌ساخت که غلط تایپی هم شماره‌ی «موفق» تولید
/// می‌کرد - این جدول مرجع، اعتبارسنجی واقعی را ممکن می‌کند. Document.DocumentType عمداً هنوز
/// رشتهٔ آزاد باقی مانده (تبدیل آن به FK یک تغییر بزرگ‌تر و پرریسک‌تر در کل برنامه است، خارج
/// از دامنهٔ این فاز)؛ این جدول فقط برای اعتبارسنجی/تطبیق استفاده می‌شود.
/// </summary>
public class DocumentTypeCode
{
    public int DocumentTypeCodeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // مشابه Discipline.ImportAliases: نام‌های رایج جایگزین برای تطبیق هنگام Import/شماره‌گذاری
    public string? ImportAliases { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }
}
