using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class ElectricalEquipment
{
    [Key]
    public int ElectricalEquipmentId { get; set; }

    // ارتباط با تجهیز اصلی (در صورت وجود)
    public int? EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // شماره تگ
    public string TagNumber { get; set; } = string.Empty;

    // نوع تجهیز
    // Motor, Transformer, MCC, Switchgear
    public string EquipmentType { get; set; } = string.Empty;

    // سازنده
    public string? Manufacturer { get; set; }

    // مدل
    public string? Model { get; set; }

    // توان
    public decimal? PowerKW { get; set; }

    // ولتاژ
    public decimal? Voltage { get; set; }

    // جریان نامی
    public decimal? RatedCurrent { get; set; }

    // تعداد فاز
    public int? Phase { get; set; }

    // سیستم تغذیه
    public string? SupplySource { get; set; }

    // توضیحات
    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}