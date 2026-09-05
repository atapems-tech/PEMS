using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// گیرنده‌ی اضافی یک نامه (طبق درخواست: پشتیبانی از "نامه" (گیرنده‌ی اصلی) در برابر "رونوشت"
/// (CC) - دقیقاً مثل نمونه‌ی کارتابل مرجع). یک نامه می‌تواند چند گیرنده‌ی اصلی/رونوشت داشته باشد،
/// هرکدام با وضعیت خوانده/نخوانده‌ی مستقل.
/// </summary>
public class LetterRecipient : BaseEntity
{
    [Key]
    public int LetterRecipientId { get; set; }

    public int LetterId { get; set; }
    public Letter? Letter { get; set; }

    // To (گیرنده‌ی اصلی - "نامه") یا Cc (رونوشت)
    public string RecipientType { get; set; } = "To";

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // برای گیرنده‌ی بیرونی (مثلاً یک تماس Vendor) که کارمند داخلی نیست
    public string? ExternalName { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
