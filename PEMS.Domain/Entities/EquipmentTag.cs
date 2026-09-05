using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class EquipmentTag
{
    [Key]
    public int TagId { get; set; }

    // تجهیز مرتبط
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // شماره تگ
    public string TagNumber { get; set; } = string.Empty;

    // نوع تگ
    // Pump, Valve, Instrument, Motor ...
    public string TagType { get; set; } = string.Empty;

    // ناحیه یا Area
    public string? Area { get; set; }

    // سرویس تجهیز
    public string? Service { get; set; }

    // شماره لوپ کنترل
    public string? LoopNumber { get; set; }

    // توضیحات
    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}