using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class CauseEffect
{
    [Key]
    public int CauseEffectId { get; set; }

    // آلارم یا سیگنال ایجاد کننده
    public int AlarmId { get; set; }

    public Alarm? Alarm { get; set; }

    // شرح علت
    public string Cause { get; set; } = string.Empty;

    // شرح نتیجه
    public string Effect { get; set; } = string.Empty;

    // نوع عملکرد
    // Trip, Shutdown, Alarm, Start, Stop
    public string? ActionType { get; set; }

    // سیگنال خروجی
    // Digital Output / Relay / PLC Signal
    public string? TripSignal { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}