using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// تاریخچه‌ی کالیبراسیون ابزار دقیق - طبق «Instrument Equipment Passport»: این جدول
/// Append-only است (هرگز رکورد قدیمی Overwrite/حذف نمی‌شود)، دقیقاً مثل MaintenanceHistory.
/// </summary>
public class CalibrationHistory
{
    [Key]
    public int CalibrationHistoryId { get; set; }

    public int InstrumentId { get; set; }

    public Instrument? Instrument { get; set; }

    public DateTime CalibrationDate { get; set; }

    // نوع کالیبراسیون - Routine, PostRepair, PreCommissioning ...
    public string CalibrationType { get; set; } = "Routine";

    // مرجع استاندارد/دستگاه کالیبراتور استفاده‌شده
    public string? StandardReference { get; set; }

    public string? RangeUnit { get; set; }

    // مقدار قرائت‌شده پیش از تنظیم (As Found)
    public string? AsFound { get; set; }

    // مقدار پس از تنظیم (As Left)
    public string? AsLeft { get; set; }

    // نتیجه - Pass, Fail, Adjusted
    public string Result { get; set; } = "Pass";

    public int? TechnicianEmployeeId { get; set; }

    public Employee? Technician { get; set; }

    public string? CertificateNo { get; set; }

    // سررسید کالیبراسیون بعدی - از این رکورد محاسبه و روی Instrument.NextCalibrationDue کش می‌شود
    public DateTime? NextDue { get; set; }

    public string? Remarks { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
