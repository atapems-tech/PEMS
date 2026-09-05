using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class Valve
{
    [Key]
    public int ValveId { get; set; }

    // شماره تگ شیر
    public string TagNumber { get; set; } = string.Empty;

    // نوع شیر
    // Ball, Gate, Globe, Butterfly, Control
    public string ValveType { get; set; } = string.Empty;

    // سایز
    public string? Size { get; set; }

    // کلاس فشاری
    // 150, 300, 600
    public string? Rating { get; set; }

    // جنس بدنه
    public string? Material { get; set; }

    // نوع اکچویتور
    // Manual, Pneumatic, Electric, Hydraulic
    public string? ActuatorType { get; set; }

    // وضعیت Fail
    // Fail Open, Fail Close, Fail Last
    public string? FailPosition { get; set; }

    // شماره خط مرتبط
    public string? LineNumber { get; set; }

    // سازنده
    public string? Manufacturer { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}