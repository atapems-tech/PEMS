namespace PEMS.Application.DTOs;

/// <summary>
/// ردیف Master Instrument List - طبق سند «PEMS — Global Data Grid Standard»: فقط فیلدهای
/// نمایش‌داده‌شده در Grid، نه کل Entity Graph (Instrument+Equipment). Status اینجا از
/// NextCalibrationDue در همان کوئری SQL محاسبه می‌شود (نه بعد از بارگذاری در حافظه).
/// </summary>
public class InstrumentListDto
{
    public int InstrumentId { get; set; }
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string InstrumentType { get; set; } = "";
    public string? LoopNumber { get; set; }
    public string? System { get; set; }
    public DateTime? LastCalibrationDate { get; set; }
    public DateTime? NextCalibrationDue { get; set; }
    public string CalibrationStatus { get; set; } = "";
}

/// <summary>
/// شمارنده‌های داشبورد Master Instrument List - طبق بند A-5 فاز ۵: باید در SQL محاسبه شوند
/// (CountAsync)، نه با بارگذاری کل جدول و Count() در حافظه. دقیقاً هم‌معنا با شمارنده‌های
/// قبلی InstrumentController.Index: روی همان فیلتر جستجو (q) حساب می‌شوند، نه روی فیلتر Status
/// (چون Status خودش یکی از گزینه‌های همین شمارنده‌هاست).
/// </summary>
public class InstrumentDashboardDto
{
    public int TotalCount { get; set; }
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
}
