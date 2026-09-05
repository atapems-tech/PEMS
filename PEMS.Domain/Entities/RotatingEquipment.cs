using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class RotatingEquipment
{
    [Key]
    public int RotatingEquipmentId { get; set; }

    // ارتباط با Mechanical Equipment
    public int MechanicalEquipmentId { get; set; }

    public MechanicalEquipment? MechanicalEquipment { get; set; }

    // نوع تجهیز
    // Pump, Compressor, Fan, Blower
    public string EquipmentType { get; set; } = string.Empty;

    // دبی
    public decimal? FlowRate { get; set; }

    // هد پمپ
    public decimal? Head { get; set; }

    // سرعت دورانی
    public int? SpeedRPM { get; set; }

    // توان
    public decimal? PowerKW { get; set; }

    // نوع درایور
    // Motor, Turbine, Engine
    public string? DriverType { get; set; }

    // نوع بیرینگ
    public string? BearingType { get; set; }

    // سیستم روانکاری
    public string? LubricationType { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}