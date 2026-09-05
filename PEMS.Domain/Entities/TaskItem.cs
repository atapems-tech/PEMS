using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// وظیفه‌ی مستقل (طبق نمونه‌ی کارتابل مرجع Pargar/Fonix: "اقدام وظایف" و "پیگیری وظایف") -
/// برخلاف Referral که همیشه به یک نامه وصل است، این می‌تواند کاملاً مستقل باشد یا اختیاری
/// به یک نامه/مدرک/هر موجودیت دیگری وصل شود (RelatedType/RelatedId، همان الگوی Notification).
/// </summary>
public class TaskItem : BaseEntity
{
    [Key]
    public int TaskItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // کسی که باید اقدام کند ("اقدام وظایف" از دید او)
    public int? AssignedToEmployeeId { get; set; }
    public Employee? AssignedToEmployee { get; set; }

    // کسی که وظیفه را محول کرده و پیگیری می‌کند ("پیگیری وظایف" از دید او)
    public int? AssignedByApplicationUserId { get; set; }
    public string? AssignedByName { get; set; }

    public DateTime? DueDate { get; set; }

    // Low, Normal, High, Urgent
    public string Priority { get; set; } = "Normal";

    // Open, InProgress, Completed, Cancelled
    public string Status { get; set; } = "Open";

    // پیوند اختیاری به هر موجودیت دیگر (نامه، مدرک، VDR و ...) - طبق همان الگوی Notification
    public string? RelatedType { get; set; }
    public int? RelatedId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? CompletedDate { get; set; }

    public string? CompletionNote { get; set; }
}
