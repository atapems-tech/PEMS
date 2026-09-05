using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class PipingLine
{
    [Key]
    public int PipingLineId { get; set; }

    // شماره خط
    // مثال: 6"-CW-101-CS
    public string LineNumber { get; set; } = string.Empty;

    // سرویس خط
    // Cooling Water, Steam, Gas ...
    public string? Service { get; set; }

    // سیال
    public string? Fluid { get; set; }

    // سایز لوله
    // مثال: 6 inch
    public string? Size { get; set; }

    // کلاس لوله
    // مثال: CS150, SS300
    public string? PipeClass { get; set; }

    // فشار طراحی
    public decimal? DesignPressure { get; set; }

    // دمای طراحی
    public decimal? DesignTemperature { get; set; }

    // تجهیز مبدا
    public string? FromEquipment { get; set; }

    // تجهیز مقصد
    public string? ToEquipment { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}