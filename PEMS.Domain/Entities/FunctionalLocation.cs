using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class FunctionalLocation
{
    [Key]
    public int FunctionalLocationId { get; set; }

    // کد محل نصب
    // مثال: U100-CW-PUMP
    public string Code { get; set; } = string.Empty;

    // نام محل
    public string Name { get; set; } = string.Empty;

    // کارخانه
    public string? Plant { get; set; }

    // ناحیه
    public string? Area { get; set; }

    // یونیت
    public string? Unit { get; set; }

    // ساختار درختی
    // Parent Functional Location
    public int? ParentId { get; set; }

    public FunctionalLocation? Parent { get; set; }

    public ICollection<FunctionalLocation> Children { get; set; }
        = new List<FunctionalLocation>();

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}