using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class Cable
{
    [Key]
    public int CableId { get; set; }

    // شماره کابل
    public string CableNumber { get; set; } = string.Empty;

    // نوع کابل
    // Power, Control, Instrument, Fiber
    public string CableType { get; set; } = string.Empty;

    // مبدا
    public string? FromEquipment { get; set; }

    // مقصد
    public string? ToEquipment { get; set; }

    // سطح ولتاژ
    public string? VoltageLevel { get; set; }

    // طول کابل
    public decimal? Length { get; set; }

    // سایز کابل
    // مثال: 3x95 mm²
    public string? Size { get; set; }

    // تعداد رشته
    public int? CoreNumber { get; set; }

    // روش نصب
    // Tray, Buried, Conduit
    public string? InstallationMethod { get; set; }

    // مسیر کابل
    public string? CableTray { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}