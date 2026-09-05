using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class IOPoint
{
    [Key]
    public int IOPointId { get; set; }

    // ارتباط با Instrument
    public int InstrumentId { get; set; }

    public Instrument? Instrument { get; set; }

    // شماره تگ
    public string TagNumber { get; set; } = string.Empty;

    // نوع I/O
    // AI, AO, DI, DO
    public string IOType { get; set; } = string.Empty;

    // نوع سیگنال
    // 4-20mA, Digital, HART
    public string? SignalType { get; set; }

    // نام PLC
    public string? PLCName { get; set; }

    // شماره Rack
    public string? Rack { get; set; }

    // شماره Slot
    public string? Slot { get; set; }

    // شماره Channel
    public string? Channel { get; set; }

    // آدرس PLC
    public string? Address { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}