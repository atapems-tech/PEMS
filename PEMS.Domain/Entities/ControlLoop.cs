using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class ControlLoop
{
    [Key]
    public int LoopId { get; set; }

    // شماره Loop
    public string LoopNumber { get; set; } = string.Empty;

    // توضیحات Loop
    public string? Description { get; set; }

    // ناحیه فرآیندی
    public string? ProcessArea { get; set; }

    // نوع کنترل
    // PID, ON/OFF, Cascade
    public string? ControllerType { get; set; }

    // نام PLC یا DCS
    public string? ControllerSystem { get; set; }

    // Instrument اصلی Loop
    public int? InstrumentId { get; set; }

    public Instrument? Instrument { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}