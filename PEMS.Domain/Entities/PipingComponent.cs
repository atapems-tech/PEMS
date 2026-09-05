using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class PipingComponent
{
    [Key]
    public int PipingComponentId { get; set; }

    // شماره تگ یا شناسه قطعه
    // مثال: ELB-101
    public string TagNumber { get; set; } = string.Empty;

    // نوع قطعه
    // Elbow, Tee, Reducer, Flange, Strainer
    public string ComponentType { get; set; } = string.Empty;

    // سایز
    public string? Size { get; set; }

    // جنس متریال
    public string? Material { get; set; }

    // کلاس فشاری
    public string? Rating { get; set; }

    // شماره خط مرتبط
    public string? LineNumber { get; set; }

    // کلاس یا Specification
    public string? Specification { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}