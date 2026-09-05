using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class Instrument
{
    [Key]
    public int InstrumentId { get; set; }

    // ==============================================================================
    // طبق بازبینی: Instrument یک تخصص‌یافته (Specialization) روی همان رکورد Equipment است،
    // نه یک موجودیت مستقل با هویت جدا. TagNumber، Manufacturer، Model، Description همه از
    // Equipment می‌آیند - این‌جا دوباره ذخیره/وارد نمی‌شوند تا دو منبع حقیقت برای یک واقعیت
    // واحد نداشته باشیم. هر Equipment حداکثر یک Instrument دارد (یک‌به‌یک).
    // ==============================================================================
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // نوع ابزار
    // PT, LT, FT, TT, Control Valve ...
    public string InstrumentType { get; set; } = string.Empty;

    // رنج اندازه گیری حد پایین
    public decimal? RangeMin { get; set; }

    // رنج اندازه گیری حد بالا
    public decimal? RangeMax { get; set; }

    // واحد اندازه گیری
    public string? Unit { get; set; }

    // نوع سیگنال
    // 4-20mA, Digital, HART, FF
    public string? SignalType { get; set; }

    // دوره کالیبراسیون (روز)
    public int? CalibrationInterval { get; set; }

    // ==============================================================================
    // طبق «Instrument Equipment Passport»: فیلدهای پاسپورت ابزار دقیق که در Instrument
    // اصلی وجود نداشت. Equipment والد باقی می‌ماند؛ این‌ها فقط جزئیات اختصاصی ابزار دقیق‌اند.
    // ==============================================================================

    // شماره لوپ (Loop Number)
    public string? LoopNumber { get; set; }

    // سیستم مرتبط - DCS, ESD, F&G, PLC ...
    public string? System { get; set; }

    // کلاس سیگنال - AI, AO, DI, DO ...
    public string? SignalClass { get; set; }

    // متغیر فرآیندی اندازه‌گیری‌شده (Pressure, Level, Flow, Temperature ...)
    public string? ProcessVariable { get; set; }

    // دقت اندازه‌گیری (٪ Span یا مقدار)
    public string? Accuracy { get; set; }

    // سیگنال خروجی - 4-20mA, HART, Fieldbus, Digital ...
    public string? OutputSignal { get; set; }

    // منبع تغذیه
    public string? PowerSupply { get; set; }

    // پروتکل ارتباطی - HART, Foundation Fieldbus, Profibus ...
    public string? Communication { get; set; }

    // درجه حفاظت محفظه - IP65, IP66 ...
    public string? IPRating { get; set; }

    // نوع اتصال فرآیندی
    public string? ProcessConnection { get; set; }

    // نوع اتصال الکتریکی
    public string? ElectricalConnection { get; set; }

    // ناحیه خطرناک - Zone 1, Zone 2, Non-Hazardous ...
    public string? HazardousArea { get; set; }

    // نوع حفاظت اکس‌پروف - Exd, Exia, Exe ...
    public string? ExProtection { get; set; }

    public string? Zone { get; set; }

    public string? GasGroup { get; set; }

    public string? TempClass { get; set; }

    // وضعیت Fail-Safe - Fail Open, Fail Close, Fail Last Position ...
    public string? FailAction { get; set; }

    public decimal? AlarmSetpoint { get; set; }

    public decimal? TripSetpoint { get; set; }

    // سطح یکپارچگی ایمنی - SIL1, SIL2, SIL3
    public string? SIL { get; set; }

    public string? JunctionBox { get; set; }

    public string? MarshallingCabinet { get; set; }

    public string? CableNumber { get; set; }

    public string? BarrierRelay { get; set; }

    // کارت/کانال ورودی-خروجی در سیستم کنترل
    public string? IOCardChannel { get; set; }

    public string? SystemCabinet { get; set; }

    // ==============================================================================
    // تاریخ‌های کالیبراسیون/نگهداری - Next بر اساس آخرین رکورد CalibrationHistory/
    // MaintenanceHistory محاسبه و کش می‌شود؛ منبع حقیقت همان تاریخچه‌هاست.
    // ==============================================================================

    public DateTime? LastCalibrationDate { get; set; }

    public DateTime? NextCalibrationDue { get; set; }

    public DateTime? LastMaintenanceDate { get; set; }

    public DateTime? NextMaintenanceDue { get; set; }

    // وضعیت فعلی - InService, OutOfService, UnderCalibration, UnderMaintenance, Decommissioned
    public string CurrentStatus { get; set; } = "InService";

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}