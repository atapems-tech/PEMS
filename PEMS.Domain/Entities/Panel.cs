using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class Panel
{
    [Key]
    public int PanelId { get; set; }

    // شماره تابلو
    public string PanelNumber { get; set; } = string.Empty;

    // نوع تابلو
    // MCC, PLC Panel, JB, LCP
    public string PanelType { get; set; } = string.Empty;

    // محل نصب
    public string? Location { get; set; }

    // سازنده
    public string? Manufacturer { get; set; }

    // ولتاژ
    public string? Voltage { get; set; }

    // درجه حفاظت
    // IP54, IP65 ...
    public string? IPRating { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}