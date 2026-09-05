using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class Alarm
{
    [Key]
    public int AlarmId { get; set; }

    // ارتباط با Instrument
    public int InstrumentId { get; set; }

    public Instrument? Instrument { get; set; }

    // شماره تگ آلارم
    public string AlarmTag { get; set; } = string.Empty;

    // نوع آلارم
    // High, Low, HighHigh, LowLow
    public string AlarmType { get; set; } = string.Empty;

    // اولویت
    // Low, Medium, High, Critical
    public string Priority { get; set; } = string.Empty;

    // مقدار تنظیم آلارم
    public decimal? SetPoint { get; set; }

    // واحد اندازه گیری
    public string? Unit { get; set; }

    // شرح آلارم
    public string? Description { get; set; }

    // اقدام مورد نیاز
    public string? Action { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}