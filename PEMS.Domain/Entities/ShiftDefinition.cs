using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش AF/AR: حداقل مفهوم شیفت مورد
/// نیاز تولید - فقط تعریف بازه‌ی زمانی. عمداً یک سیستم کامل برنامه‌ریزی نیروی کار (تخصیص
/// کارمند به شیفت در تقویم) نیست - همان محدوده‌ای که ShiftHandover هم صراحتاً از آن پرهیز کرده
/// (به‌موجب کامنت خودِ آن Entity). هیچ فرض تقویم/تاریخ ایرانی در این سطح دامنه وارد نشده -
/// StartTime/EndTime ساعت روزانه‌اند، نه تاریخ کامل.
/// </summary>
public class ShiftDefinition : BaseEntity
{
    public int ShiftDefinitionId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
