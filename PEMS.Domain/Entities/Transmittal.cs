using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک بسته‌ی ارسال رسمی مدارک مهندسی به یک گیرنده (کارفرما، پیمانکار، دیسیپلین دیگر و ...).
///
/// طبق ممیزی حاکمیت Transmittal (بخش G): پس از Status != "Draft" (یعنی "Sent" یا بالاتر)،
/// شماره/تاریخ/مدارک/گیرندگان/هدف صدور دیگر قابل تغییر بی‌صدا نیستند - هیچ اکشن Edit روی
/// ترانسمیتال ارسال‌شده وجود ندارد (فقط Cancel و سپس Reissue که رکورد جدید می‌سازد، بدون
/// دستکاری تاریخچه‌ی رکورد قدیمی). RowVersion (از طریق SaveChanges عمومی) تضمین می‌کند دو
/// کاربر هم‌زمان نتوانند یک رکورد را ناهماهنگ ذخیره کنند.
/// </summary>
public class Transmittal
{
    [Key]
    public int TransmittalId { get; set; }

    public string TransmittalNo { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    // For Approval / For Construction / For Information / For Review / IFA / IFR / IFI / FI
    public string Purpose { get; set; } = "For Information";

    public string? Description { get; set; }

    // Draft / Sent / Acknowledged / Cancelled
    public string Status { get; set; } = "Draft";

    // تاریخ ایجاد رکورد (پیش‌نویس) - جدا از SentDate که لحظه‌ی صدور رسمی واقعی است
    public DateTime TransmittalDate { get; set; } = DateTime.Now;

    // لحظه‌ی واقعی صدور رسمی (Status از Draft به Sent تغییر کرد) - قبلاً این لحظه اصلاً ثبت
    // نمی‌شد و TransmittalDate (زمان ایجاد پیش‌نویس) به‌اشتباه به‌جایش استفاده می‌شد
    public DateTime? SentDate { get; set; }

    public int? SentById { get; set; }
    public Employee? SentBy { get; set; }

    // طبق بخش ۵ (Response Required): این‌جا یک فیلد دامنه‌ای واقعی است، نه فقط چک‌باکس UI -
    // TransmittalService.MarkSentAsync وقتی true باشد، ResponseDueDate را از روی
    // WorkingDayCalculator موجود محاسبه و اجباراً ثبت می‌کند (نه Naive Calendar-Day)
    public bool ResponseRequired { get; set; }

    public DateTime? ResponseDueDate { get; set; }

    public DateTime? ResponseReceivedDate { get; set; }

    public string? ResponseNote { get; set; }

    public DateTime? CancelledDate { get; set; }
    public string? CancelledReason { get; set; }
    public int? CancelledById { get; set; }
    public Employee? CancelledBy { get; set; }

    // طبق بخش ۸ (Immutability): اصلاح یک ترانسمیتال صادرشده با ویرایش بی‌صدا انجام نمی‌شود -
    // به‌جایش ترانسمیتال قبلی Cancel و یک رکورد جدید Draft با همین زنجیره ساخته می‌شود (دقیقاً
    // هم‌الگو با DocumentRevision.SupersededRevisionId)
    public int? SupersedesTransmittalId { get; set; }
    public Transmittal? SupersedesTransmittal { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<TransmittalItem> Items { get; set; } = new List<TransmittalItem>();

    public ICollection<TransmittalRecipient> Recipients { get; set; } = new List<TransmittalRecipient>();

    [NotMapped]
    public bool IsIssued => Status is "Sent" or "Acknowledged";

    [NotMapped]
    public bool IsResponseOverdue =>
        ResponseRequired && ResponseDueDate.HasValue && ResponseReceivedDate == null && DateTime.Now > ResponseDueDate.Value;
}
