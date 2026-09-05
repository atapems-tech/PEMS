using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class EquipmentDatasheet
{
    [Key]
    public int DatasheetId { get; set; }

    // ارتباط با تجهیز
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // شماره مدرک Datasheet
    public string DocumentNumber { get; set; } = string.Empty;

    // Revision
    public string RevisionNo { get; set; } = "00";

    // فشار طراحی
    public decimal? DesignPressure { get; set; }

    // دمای طراحی
    public decimal? DesignTemperature { get; set; }

    // فشار عملیاتی
    public decimal? OperatingPressure { get; set; }

    // دمای عملیاتی
    public decimal? OperatingTemperature { get; set; }

    // سازنده
    public string? Manufacturer { get; set; }

    // مدل
    public string? Model { get; set; }

    // مشخصات تکمیلی
    public string? Specification { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}