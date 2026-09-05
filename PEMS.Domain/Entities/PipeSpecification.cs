using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class PipeSpecification
{
    [Key]
    public int PipeSpecificationId { get; set; }

    // کد Spec
    // مثال: CS150, SS300
    public string SpecCode { get; set; } = string.Empty;

    // توضیحات
    public string? Description { get; set; }

    // جنس متریال
    // Carbon Steel, Stainless Steel
    public string? Material { get; set; }

    // کلاس فشاری
    // 150, 300, 600
    public string? Rating { get; set; }

    // فشار طراحی
    public decimal? DesignPressure { get; set; }

    // دمای طراحی
    public decimal? DesignTemperature { get; set; }

    // سرویس
    // Steam, Cooling Water, Hydrocarbon
    public string? Service { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}