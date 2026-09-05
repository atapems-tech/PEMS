using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class MechanicalEquipment
{
    [Key]
    public int MechanicalEquipmentId { get; set; }

    // ارتباط با تجهیز اصلی
    public int? EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // شماره تگ
    public string TagNumber { get; set; } = string.Empty;

    // نوع تجهیز
    // Pump, Compressor, Vessel, Tank, Heat Exchanger
    public string EquipmentType { get; set; } = string.Empty;

    // فشار طراحی
    public decimal? DesignPressure { get; set; }

    // دمای طراحی
    public decimal? DesignTemperature { get; set; }

    // فشار عملیاتی
    public decimal? OperatingPressure { get; set; }

    // دمای عملیاتی
    public decimal? OperatingTemperature { get; set; }

    // جنس متریال
    public string? Material { get; set; }

    // سازنده
    public string? Manufacturer { get; set; }

    // مدل
    public string? Model { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}