using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class StaticEquipment
{
    [Key]
    public int StaticEquipmentId { get; set; }

    // ارتباط با Mechanical Equipment
    public int MechanicalEquipmentId { get; set; }

    public MechanicalEquipment? MechanicalEquipment { get; set; }

    // نوع تجهیز
    // Vessel, Tank, Column, Heat Exchanger, Filter
    public string EquipmentType { get; set; } = string.Empty;

    // حجم تجهیز
    public decimal? Volume { get; set; }

    // فشار طراحی
    public decimal? DesignPressure { get; set; }

    // دمای طراحی
    public decimal? DesignTemperature { get; set; }

    // جنس متریال
    public string? Material { get; set; }

    // حالت نصب
    // Vertical, Horizontal
    public string? Orientation { get; set; }

    // استاندارد طراحی
    // ASME, API
    public string? CodeStandard { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}