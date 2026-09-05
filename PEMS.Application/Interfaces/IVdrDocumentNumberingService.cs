namespace PEMS.Application.Interfaces;

/// <summary>
/// شماره‌گذاری رسمی مدرک فروشنده به فرمت CONTRACTOR طبق «VENDOR DOCUMENTATION MANAGEMENT
/// PROCEDURE» بخش ۴.۳.۱: Plant-Unit-VendorCode-DisciplineCode-DocType-MaterialCode-MRLast4-Sequence-Revision
/// (نمونه سند: J-85-FSK-ME-DSH-0611-0001-001-0). فقط برای VDRItem‌هایی کار می‌کند که از قبل به
/// یک MR واقعی (نه فقط شماره‌ی خام متنی) وصل شده باشند - طبق فلسفه‌ی موجود پروژه («لینک واقعی
/// فقط بعد از تأیید انسانی در Relationship Explorer»).
/// </summary>
public interface IVdrDocumentNumberingService
{
    Task<string> GenerateContractorNumberAsync(int vdrItemId);

    /// <summary>
    /// فرمت SUB-CONTRACTOR/Vendor طبق بخش ۴.۳.۲: 3863-XX-VD-TT-UU-MMMM-NNNNNN-YYY
    /// (Job-DisciplineCode-"VD"-TypologyCode-Unit-MRLast4-EquipmentTagLast6-Sequence).
    /// </summary>
    Task<string> GenerateVendorNumberAsync(int vdrItemId);
}
