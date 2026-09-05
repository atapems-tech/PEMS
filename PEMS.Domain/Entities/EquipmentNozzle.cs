using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class EquipmentNozzle
{
    [Key]
    public int EquipmentNozzleId { get; set; }

    // ارتباط با Equipment اصلی
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // شماره نازل
    // مثال: N1, N2
    public string NozzleTag { get; set; } = string.Empty;

    // سایز نازل
    public string? Size { get; set; }

    // کلاس فشاری
    public string? Rating { get; set; }

    // نوع اتصال
    // Inlet, Outlet, Vent, Drain
    public string? Type { get; set; }

    // سرویس
    public string? Service { get; set; }

    // جهت نصب
    // Top, Bottom, Side
    public string? Orientation { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}